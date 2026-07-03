using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services;

/// <summary>When the staff table is empty, inserts the default Admin test account (PIN 1234).</summary>
public interface IDefaultStaffBootstrapper
{
    Task EnsureDefaultStaffIfEmptyAsync(CancellationToken cancellationToken = default);
}
