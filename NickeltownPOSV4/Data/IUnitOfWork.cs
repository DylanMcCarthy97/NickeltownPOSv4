using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
