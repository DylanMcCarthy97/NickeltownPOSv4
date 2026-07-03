using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;

namespace NickeltownPOSV4.Data.Migration;

public interface ISquareConfigMigrationRepository
{
    Task ImportSquareConfigAsync(LegacySquareConfigDto config, CancellationToken cancellationToken = default);
}
