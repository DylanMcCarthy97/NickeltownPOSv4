using System;
using CommunityToolkit.Mvvm.ComponentModel;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.Stock;

namespace NickeltownPOSV4.ViewModels;

public sealed class StockListRowViewModel : ObservableObject
{
    private bool _isSelected;

    public StockListRowViewModel(StockEditorRow row, bool isAlternateRow = false)
    {
        IsAlternateRow = isAlternateRow;
        ItemId = row.ItemId;
        Name = row.Name;
        CategoryLine = StockInventoryLevelHelper.BuildCategoryLine(row);
        StockQty = row.StockQty;
        StockHeroText = StockInventoryLevelHelper.FormatStockHero(row);
        StatusText = StockInventoryLevelHelper.StatusDisplayText(row);
        StatusAccentColor = StockInventoryLevelHelper.StatusAccentColor(row);
        StatusPillBackground = ToLightTint(StatusAccentColor);
        var resolved = StockItemImageResolver.TryResolve(
            row.ImagePath,
            row.RawJson,
            row.CatalogSubCategory,
            allowBarcodeLookup: true,
            sku: row.Sku);
        ProductImagePath = resolved ?? row.ImagePath;
        ProductImageFallbackEmoji = StockItemImageResolver.GetDisplayEmoji(row.ImagePath, row.CatalogSubCategory);
        ShowStatusPill = StockInventoryLevelHelper.ShowStatusPill(row);
        CanQuickAdjustStock = StockInventoryLevelHelper.CanQuickAdjustStock(row);
    }

    public long ItemId { get; }
    public string Name { get; }
    public string CategoryLine { get; }
    public int StockQty { get; }
    public string StockHeroText { get; }
    public string StatusText { get; }
    public string StatusAccentColor { get; }
    public string StatusPillBackground { get; }
    public string? ProductImagePath { get; }
    public string ProductImageFallbackEmoji { get; }
    public bool ShowStatusPill { get; }
    public bool CanQuickAdjustStock { get; }

    public bool IsAlternateRow { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private static string ToLightTint(string accent)
    {
        if (accent.Length >= 7 && accent.StartsWith("#FF", StringComparison.Ordinal))
        {
            return "#1A" + accent.Substring(3);
        }

        return "#1A6B7280";
    }
}

public sealed class StockShoppingListRowViewModel
{
    public StockShoppingListRowViewModel(StockEditorRow row)
    {
        ItemId = row.ItemId;
        Name = row.Name;
        CategoryLine = StockInventoryLevelHelper.BuildCategoryLine(row);
        HaveQty = row.StockQty;
        UnitsToBuy = StockInventoryLevelHelper.NeedQty(row);
        NeedQty = UnitsToBuy;
        Status = StockInventoryLevelHelper.ResolveStatus(row);
        PacksToBuy = StockInventoryLevelHelper.ResolvePacksToBuy(row);
        SuggestedLine = PacksToBuy is int packs
            ? packs + " pack" + (packs == 1 ? string.Empty : "s")
            : string.Empty;
        HasSetupWarning = PacksToBuy is null && UnitsToBuy > 0;
        SetupHint = string.Empty; // Pack-size note is a single footer list, not per-row spam.
        StatusText = StockInventoryLevelHelper.StatusDisplayText(row);
        StatusAccentColor = StockInventoryLevelHelper.StatusAccentColor(row);
        StatusPillBackground = ToLightTint(StatusAccentColor);
        IsMerch = StockInventoryLevelHelper.IsMerchItem(row);
        SortPriority = Status switch
        {
            StockVolunteerStatus.OutOfStock => 0,
            StockVolunteerStatus.BuyNow => 1,
            StockVolunteerStatus.BuySoon => 2,
            _ => 3,
        };
    }

    public long ItemId { get; }
    public string Name { get; }
    public string CategoryLine { get; }
    public int HaveQty { get; }

    /// <summary>Individual units required to reach target (never recommended as zero on a purchase list).</summary>
    public int UnitsToBuy { get; }

    /// <summary>Alias of <see cref="UnitsToBuy"/> for older call sites.</summary>
    public int NeedQty { get; }

    /// <summary>Whole packs/cartons to buy when pack size is configured; otherwise null.</summary>
    public int? PacksToBuy { get; }

    public StockVolunteerStatus Status { get; }
    public int SortPriority { get; }
    public string StatusText { get; }
    public string StatusAccentColor { get; }
    public string StatusPillBackground { get; }
    public bool IsMerch { get; }
    public string HaveLine => "In stock: " + HaveQty;
    public string NeedLine => "Buy: " + UnitsToBuy;
    public string SuggestedLine { get; }
    public bool HasSetupWarning { get; }
    public string SetupHint { get; }
    public string SuggestedDisplayLine => string.IsNullOrEmpty(SuggestedLine)
        ? string.Empty
        : "Packs: " + SuggestedLine;

    private static string ToLightTint(string accent)
    {
        if (accent.Length >= 7 && accent.StartsWith("#FF", StringComparison.Ordinal))
        {
            return "#1A" + accent.Substring(3);
        }

        return "#1A6B7280";
    }
}