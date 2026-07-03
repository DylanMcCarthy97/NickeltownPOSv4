using System;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services.Stock;

/// <summary>Volunteer stock checklist — shared status, need-buying, and shopping-list rules.</summary>
public static class StockInventoryLevelHelper
{
    public const string StatusOutOfStock = "Out Of Stock";
    public const string StatusBuyNow = "Buy Now";
    public const string StatusBuySoon = "Buy Soon";
    public const string StatusPlenty = "Plenty";
    public const string StatusSellUntilGone = "Sell Until Gone";
    public const string StatusGone = "Gone";

    public static bool IsTrackedStockItem(StockEditorRow row) =>
        row.IsActive != 0 && row.TrackStock != 0 && row.OrderInMerchandise == 0;

    public static bool IsSellUntilGoneItem(StockEditorRow row)
    {
        if (row.IsActive == 0)
        {
            return false;
        }

        var mode = StockCatalogTaxonomy.NormalizeStockMode(row.StockMode);
        return mode == StockCatalogTaxonomy.StockModeSellUntilGone
            || row.NotGonnaOrderBack != 0
            || row.IsRunOutItem != 0;
    }

    /// <summary>Normal reorder stock (excludes sell-until-gone / run-out).</summary>
    public static bool IsEligibleForBuyingLogic(StockEditorRow row) =>
        IsTrackedStockItem(row) && !IsSellUntilGoneItem(row);

    public static int? ResolveWarnBelow(StockEditorRow row)
    {
        if (row.WarnMeBelow is > 0)
        {
            return row.WarnMeBelow;
        }

        return row.LowStockThreshold is > 0 ? row.LowStockThreshold : null;
    }

    public static int? ResolveTargetAmount(StockEditorRow row)
    {
        if (row.PreferredStockLevel is > 0)
        {
            return row.PreferredStockLevel;
        }

        var meta = StockItemMetadataSerializer.Parse(row.ItemDescription, isShotMixer: false);
        if (meta.ParLevel is > 0)
        {
            return meta.ParLevel;
        }

        if (row.LowStockThreshold is > 0)
        {
            return row.LowStockThreshold * 2;
        }

        var warn = ResolveWarnBelow(row);
        if (warn is > 0)
        {
            return warn * 2;
        }

        return null;
    }

    public static int NeedQty(StockEditorRow row)
    {
        if (!IsEligibleForBuyingLogic(row))
        {
            return 0;
        }

        var target = ResolveTargetAmount(row);
        if (target is not > 0)
        {
            return 0;
        }

        return Math.Max(target.Value - row.StockQty, 0);
    }

    public static bool ShowOnShoppingList(StockEditorRow row)
    {
        if (!IsEligibleForBuyingLogic(row))
        {
            return false;
        }

        return row.ShowOnShoppingList is null || row.ShowOnShoppingList != 0;
    }

    public static StockVolunteerStatus ResolveStatus(StockEditorRow row)
    {
        if (row.IsActive == 0)
        {
            return StockVolunteerStatus.NotCounted;
        }

        if (row.OrderInMerchandise != 0 || row.TrackStock == 0)
        {
            return StockVolunteerStatus.NotCounted;
        }

        if (IsSellUntilGoneItem(row))
        {
            return row.StockQty > 0
                ? StockVolunteerStatus.SellUntilGone
                : StockVolunteerStatus.Gone;
        }

        if (!IsEligibleForBuyingLogic(row))
        {
            return StockVolunteerStatus.NotCounted;
        }

        if (row.StockQty <= 0)
        {
            return StockVolunteerStatus.OutOfStock;
        }

        var warn = ResolveWarnBelow(row);
        if (warn is > 0 && row.StockQty <= warn.Value)
        {
            return StockVolunteerStatus.BuyNow;
        }

        var target = ResolveTargetAmount(row);
        if (target is > 0 && row.StockQty < target.Value)
        {
            return StockVolunteerStatus.BuySoon;
        }

        return StockVolunteerStatus.Plenty;
    }

    public static bool NeedsBuying(StockEditorRow row)
    {
        if (!ShowOnShoppingList(row))
        {
            return false;
        }

        return ResolveStatus(row) switch
        {
            StockVolunteerStatus.OutOfStock => true,
            StockVolunteerStatus.BuyNow => true,
            StockVolunteerStatus.BuySoon => true,
            _ => false,
        };
    }

    public static bool IsShoppingListCandidate(StockEditorRow row) =>
        NeedsBuying(row);

    public static string StatusDisplayText(StockEditorRow row) =>
        ResolveStatus(row) switch
        {
            StockVolunteerStatus.OutOfStock => StatusOutOfStock,
            StockVolunteerStatus.BuyNow => StatusBuyNow,
            StockVolunteerStatus.BuySoon => StatusBuySoon,
            StockVolunteerStatus.Plenty => StatusPlenty,
            StockVolunteerStatus.SellUntilGone => StatusSellUntilGone,
            StockVolunteerStatus.Gone => StatusGone,
            _ => string.Empty,
        };

    public static string StatusAccentColor(StockEditorRow row) =>
        ResolveStatus(row) switch
        {
            StockVolunteerStatus.OutOfStock => "#FF991B1B",
            StockVolunteerStatus.BuyNow => "#FFDC2626",
            StockVolunteerStatus.BuySoon => "#FFF59E0B",
            StockVolunteerStatus.Plenty => "#FF16A34A",
            StockVolunteerStatus.SellUntilGone => "#FF7C3AED",
            StockVolunteerStatus.Gone => "#FF9CA3AF",
            _ => "#FF6B7280",
        };

    public static string FormatSuggestedLine(StockEditorRow row)
    {
        var need = NeedQty(row);
        if (need <= 0 && ResolveStatus(row) == StockVolunteerStatus.OutOfStock)
        {
            var target = ResolveTargetAmount(row);
            if (target is > 0)
            {
                need = target.Value;
            }
        }

        if (need <= 0)
        {
            return string.Empty;
        }

        var pack = row.PurchaseUnitQty;
        if (pack is not > 0)
        {
            return "Pack size not set";
        }

        var packs = (int)Math.Ceiling(need / (double)pack.Value);
        return packs + " x " + pack.Value + " Pack";
    }

    public static string FormatSuggestedBuy(StockEditorRow row) => FormatSuggestedLine(row);

    public static bool IsMerchItem(StockEditorRow row) =>
        (row.CatalogSubCategory ?? string.Empty).Trim().Equals("Merch", StringComparison.OrdinalIgnoreCase);

    public static string BuildCategoryLine(StockEditorRow row) =>
        (row.CatalogSubCategory ?? "Drinks").Trim();

    public static string FormatStockHero(StockEditorRow row) =>
        row.StockQty + " LEFT";

    public static bool ShowStatusPill(StockEditorRow row) =>
        ResolveStatus(row) != StockVolunteerStatus.NotCounted;

    public static bool CanQuickAdjustStock(StockEditorRow row) =>
        row.IsActive != 0 && row.TrackStock != 0 && row.OrderInMerchandise == 0;
}

public enum StockVolunteerStatus
{
    NotCounted,
    OutOfStock,
    BuyNow,
    BuySoon,
    Plenty,
    SellUntilGone,
    Gone,
}
