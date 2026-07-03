using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;

namespace NickeltownPOSV4.Data.Migration;

public sealed class NullTabMigrationRepository : ITabMigrationRepository
{
    public Task ImportTabsAsync(IReadOnlyList<LegacyTabDto> tabs, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
