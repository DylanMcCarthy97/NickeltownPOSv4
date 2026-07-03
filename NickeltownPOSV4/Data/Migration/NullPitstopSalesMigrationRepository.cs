using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;

namespace NickeltownPOSV4.Data.Migration;

public sealed class NullPitstopSalesMigrationRepository : IPitstopSalesMigrationRepository
{
    public Task ImportSalesAsync(IReadOnlyList<LegacyPitstopSaleDto> sales, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
