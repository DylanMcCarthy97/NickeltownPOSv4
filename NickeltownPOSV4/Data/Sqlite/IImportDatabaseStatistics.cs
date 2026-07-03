using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Migration;

namespace NickeltownPOSV4.Data.Sqlite;

public interface IImportDatabaseStatistics
{
    Task<ImportVerificationSnapshot> BuildVerificationAsync(
        MigrationImportResult? lastRun,
        CancellationToken cancellationToken = default);
}
