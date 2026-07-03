using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.AddDrinks;

/// <summary>Persists drink sale lines to the target tab and registers undo.</summary>
public sealed class AddDrinksSaleCommitService
{
    private readonly ITabEntryService _tabEntries;
    private readonly IBarCatalogCache _barCatalogCache;
    private readonly ITabWorkspaceRefreshBus _refreshBus;
    private readonly ITabWorkspaceUndoStack _undo;

    public AddDrinksSaleCommitService(
        ITabEntryService tabEntries,
        IBarCatalogCache barCatalogCache,
        ITabWorkspaceRefreshBus refreshBus,
        ITabWorkspaceUndoStack undo)
    {
        _tabEntries = tabEntries;
        _barCatalogCache = barCatalogCache;
        _refreshBus = refreshBus;
        _undo = undo;
    }

    public async Task<AddDrinksCommitResult> CommitAsync(
        string tabLegacyId,
        IReadOnlyList<TabDrinkSaleLine> lines,
        CancellationToken cancellationToken = default)
    {
        var result = await _tabEntries.CommitDrinkSaleAsync(tabLegacyId, lines, cancellationToken).ConfigureAwait(false);
        if (!result.Ok)
        {
            return AddDrinksCommitResult.Failed(result.ErrorMessage ?? "Could not add drinks to the tab.");
        }

        _barCatalogCache.Invalidate();
        _refreshBus.RequestRefresh();

        var batchId = result.DrinkCommitBatchId;
        if (!string.IsNullOrEmpty(batchId))
        {
            var lineCount = lines.Count;
            _undo.PushUndo(
                $"Undo last drinks ({lineCount} line{(lineCount == 1 ? string.Empty : "s")})",
                async () =>
                {
                    var rev = await _tabEntries
                        .ReverseDrinkBatchAsync(tabLegacyId, batchId!, CancellationToken.None)
                        .ConfigureAwait(false);
                    if (!rev.Ok)
                    {
                        return false;
                    }

                    _refreshBus.RequestRefresh();
                    return true;
                });
        }

        return AddDrinksCommitResult.Succeeded();
    }

    public static string FormatNegativeBalanceMessage(decimal projectedBalanceAfterSale) =>
        $"This tab is now {projectedBalanceAfterSale.ToString("C2", CultureInfo.CurrentCulture)}. Add funds or review the tab.";
}

public readonly record struct AddDrinksCommitResult(bool Ok, string? ErrorMessage)
{
    public static AddDrinksCommitResult Succeeded() => new(true, null);

    public static AddDrinksCommitResult Failed(string message) => new(false, message);
}
