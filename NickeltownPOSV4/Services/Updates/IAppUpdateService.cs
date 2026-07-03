using System;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Updates;

public interface IAppUpdateService
{
    Task<AppUpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    Task<AppUpdateInstallResult> InstallUpdateAsync(
        AppUpdateManifest manifest,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
