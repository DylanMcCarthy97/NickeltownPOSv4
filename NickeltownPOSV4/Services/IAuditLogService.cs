using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Audit;

namespace NickeltownPOSV4.Services;

public interface IAuditLogService
{
    Task<long> LogAsync(AuditLogEntryRequest request, CancellationToken cancellationToken = default);

    Task<long> LogAsync(
        string actionType,
        string? entityType = null,
        string? entityId = null,
        decimal? amount = null,
        string? reason = null,
        bool success = true,
        string? detailsJson = null,
        CancellationToken cancellationToken = default);
}
