using System.Collections.Generic;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.AddDrinks;

internal static class AddDrinksCatalogProductFactory
{
    public static DrinkCardItem FromBarProduct(
        BarCatalogProductRow p,
        bool isManualFavorite,
        bool isGuestTab,
        IReadOnlyList<string> alternateSkus)
    {
        var bar = (decimal)p.BarPrice;
        var guest = (decimal)p.GuestPrice;
        var barSpecial = (decimal)p.BarSpecialPrice;
        var guestSpecial = (decimal)p.GuestSpecialPrice;
        var usesOpen = p.UsesOpenPrice != 0;
        var showSpecial = CatalogSpecialPricing.ShouldShowBarSpecialPrice(
            usesOpen,
            p.IsOnSpecial,
            bar,
            guest,
            barSpecial,
            guestSpecial,
            isGuestTab,
            out var regular,
            out var sale);

        return new DrinkCardItem(
            p.ItemId,
            p.Name,
            p.CategoryName,
            sale,
            p.StockQty,
            p.TrackStock,
            p.ImagePath,
            p.Sku,
            usesOpen,
            p.OrderInMerchandise != 0,
            (decimal)p.PitstopPrice,
            p.ItemType,
            isManualFavorite,
            alternateSkus,
            showSpecial,
            regular);
    }
}
