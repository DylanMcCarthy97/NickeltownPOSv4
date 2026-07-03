using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Migration;

/// <summary>Imports arbitrary settings/config JSON documents before they are normalized into typed V4 settings tables.</summary>
public interface IAppSettingsMigrationRepository
{
    Task ImportSettingsDocumentAsync(string relativePath, JsonDocument document, CancellationToken cancellationToken = default);
}
