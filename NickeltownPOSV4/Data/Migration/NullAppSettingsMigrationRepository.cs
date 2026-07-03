using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Migration;

public sealed class NullAppSettingsMigrationRepository : IAppSettingsMigrationRepository
{
    public Task ImportSettingsDocumentAsync(string relativePath, JsonDocument document, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
