using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Migration;

internal static class LegacyJsonDocumentReader
{
    public static async Task<(JsonDocument? Document, string? Error)> TryLoadDocumentAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var doc = await JsonDocument.ParseAsync(stream, MigrationJsonDefaults.DocumentOptions, cancellationToken).ConfigureAwait(false);
            return (doc, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    public static async Task<string> ComputeSha256HexAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}
