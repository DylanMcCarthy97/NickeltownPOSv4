using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;

namespace NickeltownPOSV4.Data.Migration;

public sealed class NullDrinkMigrationRepository : IDrinkMigrationRepository
{
    public Task ImportDrinksAsync(IReadOnlyList<LegacyDrinkDto> drinks, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
