using System.Text.Json.Serialization;

namespace NickeltownPOSV4.Services.Updates;

public sealed class AppUpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("packageUri")]
    public string PackageUri { get; set; } = string.Empty;

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; }
}
