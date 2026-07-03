using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Audit;

namespace NickeltownPOSV4.Data.Sqlite;

public interface IAuditLogRepository
{
    Task<long> InsertAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int maxEntries, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogEntry>> GetForEntityAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);
}
