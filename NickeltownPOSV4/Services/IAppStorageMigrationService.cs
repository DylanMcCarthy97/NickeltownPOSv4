using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services;

public interface IAppStorageMigrationService
{
    IReadOnlyList<string> FindLegacyDatabasePaths();
    IReadOnlyList<string> FindLegacySquareConfigPaths();
    IReadOnlyList<string> FindLegacyProductImageFolders();
    Task<bool> CopyDatabaseAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task<bool> CopySquareConfigAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task<int> CopyProductImagesFromFolderAsync(string sourceFolder, CancellationToken cancellationToken = default);
}
