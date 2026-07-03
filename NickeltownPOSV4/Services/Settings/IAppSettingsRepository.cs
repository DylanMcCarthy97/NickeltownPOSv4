using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Settings;

/// <summary>Typed read/write over the SQLite <c>Settings</c> key/value table.</summary>
public interface IAppSettingsRepository
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    Task SetAsync<T>(string key, T value, bool isSecret = false, CancellationToken cancellationToken = default) where T : class;
}
