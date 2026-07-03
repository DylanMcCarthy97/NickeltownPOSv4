using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Settings;

public interface IBackupService
{
    /// <summary>Creates a timestamped zip of the SQLite database into <paramref name="destinationFolder"/>; returns the full zip path.</summary>
    Task<string> CreateBackupAsync(string destinationFolder, CancellationToken cancellationToken = default);

    /// <summary>Automatic backup under Documents\NickeltownPOS\Backups (database + critical config).</summary>
    Task<string?> CreateAutomaticBackupAsync(string reason, CancellationToken cancellationToken = default);
}
