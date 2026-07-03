using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;

namespace NickeltownPOSV4.Data.Migration;

public sealed class NullSquareConfigMigrationRepository : ISquareConfigMigrationRepository
{
    public Task ImportSquareConfigAsync(LegacySquareConfigDto config, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
