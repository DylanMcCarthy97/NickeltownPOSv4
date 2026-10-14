using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Stock;

public interface IShoppingListEmailService
{
    /// <summary>
    /// Saturday automated send using the current stock snapshot.
    /// Honours enable/skip settings; only the shopping-list recipient is used.
    /// </summary>
    Task<ShoppingListEmailResult> TrySendScheduledAsync(CancellationToken cancellationToken = default);

    /// <summary>Settings test send to the configured shopping-list recipient only.</summary>
    Task<ShoppingListEmailResult> SendTestAsync(CancellationToken cancellationToken = default);
}

public sealed record ShoppingListEmailResult(bool Succeeded, string Message, bool Skipped = false);
