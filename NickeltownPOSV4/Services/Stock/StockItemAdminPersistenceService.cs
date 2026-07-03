using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Stock;

/// <summary>Persists stock admin writes and shelf prices (shared by stock management VM and wizards).</summary>
public sealed class StockItemAdminPersistenceService
{
    private readonly IStockEditingService _stock;

    public StockItemAdminPersistenceService(IStockEditingService stock) => _stock = stock;

    public Task UpdateItemAdminAsync(StockItemAdminWrite write, CancellationToken cancellationToken = default) =>
        _stock.UpdateItemAdminAsync(write, cancellationToken);

    public Task UpsertPriceIfParsedAsync(long itemId, string priceKind, string? priceText, CancellationToken cancellationToken = default)
    {
        if (!StockMoneyInputParser.TryParseMoney(priceText, out var dec))
        {
            return Task.CompletedTask;
        }

        return _stock.UpsertLatestItemPriceAsync(itemId, priceKind, dec, cancellationToken);
    }

    public async Task ApplySpecialPricesAsync(
        long itemId,
        bool isOnSpecial,
        string? barSpecialText,
        string? guestSpecialText,
        string? pitstopSpecialText,
        CancellationToken cancellationToken = default)
    {
        if (!isOnSpecial)
        {
            await _stock.UpsertLatestItemPriceAsync(itemId, "BarSpecial", 0m, cancellationToken).ConfigureAwait(false);
            await _stock.UpsertLatestItemPriceAsync(itemId, "GuestSpecial", 0m, cancellationToken).ConfigureAwait(false);
            await _stock.UpsertLatestItemPriceAsync(itemId, "PitstopSpecial", 0m, cancellationToken).ConfigureAwait(false);
            return;
        }

        await UpsertPriceIfParsedAsync(itemId, "BarSpecial", barSpecialText, cancellationToken).ConfigureAwait(false);
        await UpsertPriceIfParsedAsync(itemId, "GuestSpecial", guestSpecialText, cancellationToken).ConfigureAwait(false);
        await UpsertPriceIfParsedAsync(itemId, "PitstopSpecial", pitstopSpecialText, cancellationToken).ConfigureAwait(false);
    }
}
