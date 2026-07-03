using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data;

public sealed class NullUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }
}
