using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.AddDrinks;

internal static class AddDrinksCartHelper
{
    public static SelectedDrinkRow? FindMatchingLine(
        IEnumerable<SelectedDrinkRow> rows,
        long itemId,
        string drinkName,
        bool usesOpenPrice,
        decimal unitPrice) =>
        rows.FirstOrDefault(r =>
            r.ItemId == itemId
            && string.Equals(r.DrinkName, drinkName, StringComparison.Ordinal)
            && (!usesOpenPrice || r.UnitPrice == unitPrice));

    public static void AddOrIncrementLine(
        ObservableCollection<SelectedDrinkRow> rows,
        DrinkCardItem item,
        decimal unitPrice,
        IAddDrinksSession session)
    {
        var row = FindMatchingLine(rows, item.ItemId, item.Name, item.UsesOpenPrice, unitPrice);
        if (row is null)
        {
            row = new SelectedDrinkRow(item.ItemId, item.Name, unitPrice);
            rows.Add(row);
            session.RecordSessionFavorite(item.ItemId);
            return;
        }

        row.Quantity++;
        session.RecordSessionFavorite(item.ItemId);
    }

    public static void AddOrIncrementShotMixerLine(
        ObservableCollection<SelectedDrinkRow> rows,
        long mixerItemId,
        string displayName,
        decimal unitPrice,
        IAddDrinksSession session)
    {
        var row = FindMatchingLine(rows, mixerItemId, displayName, usesOpenPrice: false, unitPrice);
        if (row is null)
        {
            rows.Add(new SelectedDrinkRow(mixerItemId, displayName, unitPrice));
            session.RecordSessionFavorite(mixerItemId);
            return;
        }

        row.Quantity++;
        session.RecordSessionFavorite(mixerItemId);
    }

    public static void SyncCatalogPageQuantities(
        IEnumerable<DrinkCardItem> catalogPage,
        IEnumerable<SelectedDrinkRow> cart)
    {
        foreach (var d in catalogPage)
        {
            var qty = cart.Where(r => r.ItemId == d.ItemId).Sum(r => r.Quantity);
            d.CartQuantity = qty;
        }
    }

    public static List<TabDrinkSaleLine> ToSaleLines(IEnumerable<SelectedDrinkRow> rows) =>
        rows
            .Select(r => new TabDrinkSaleLine
            {
                ItemId = r.ItemId,
                DisplayName = r.DrinkName,
                UnitPrice = r.UnitPrice,
                Quantity = r.Quantity,
            })
            .ToList();
}
