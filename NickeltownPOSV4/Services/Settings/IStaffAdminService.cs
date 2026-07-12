using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Settings;

public interface IStaffAdminService
{
    Task<IReadOnlyList<StaffAdminRow>> ListAsync(CancellationToken cancellationToken = default);

    Task<long> CreateAsync(string displayName, string role, string pin4, CancellationToken cancellationToken = default);

    Task UpdateAsync(long id, string displayName, string role, bool isActive, bool isDeveloper, CancellationToken cancellationToken = default);

    Task ResetPinAsync(long id, string newPin4, CancellationToken cancellationToken = default);

    Task CompleteForcedPinChangeAsync(long id, string newPin4, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed record StaffAdminRow(
    long Id,
    string? LegacyId,
    string DisplayName,
    string Role,
    bool IsActive,
    bool IsDeveloper);
