using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;

namespace NickeltownPOSV4.Data.Migration;

public sealed class NullBartenderMigrationRepository : IBartenderMigrationRepository
{
    public Task ImportBartendersAsync(IReadOnlyList<LegacyBartenderDto> bartenders, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
