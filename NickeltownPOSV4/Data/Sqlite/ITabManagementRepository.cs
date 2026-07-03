using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Data.Sqlite;

public interface ITabManagementRepository
{
    Task<bool> ExistsOpenTabDisplayNameAsync(
        string displayName,
        string? exceptLegacyId,
        CancellationToken cancellationToken = default,
        long? exceptSqliteTabId = null);

    /// <summary>Duplicate check among open <strong>member</strong> tabs only (same display/name).</summary>
    Task<bool> ExistsOpenMemberTabDisplayNameAsync(
        string displayName,
        string? exceptLegacyId,
        CancellationToken cancellationToken = default,
        long? exceptSqliteTabId = null);

    /// <summary>Next free name in the Guest 1, Guest 2, … sequence (considers all non-deleted tabs).</summary>
    Task<string> SuggestNextGuestSequenceNameAsync(CancellationToken cancellationToken = default);

    Task<TabMutationResult> CreateTabAsync(
        string displayName,
        PosTabAccountKind kind,
        decimal startingBalance,
        string? memberId,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<TabEditorRow?> GetTabEditorRowAsync(string legacyId, CancellationToken cancellationToken = default);

    Task<TabMutationResult> UpdateTabAsync(
        string legacyId,
        string displayName,
        PosTabAccountKind kind,
        string? notes,
        string? memberId,
        CancellationToken cancellationToken = default);

    Task<TabMutationResult> SetTabArchivedAsync(string legacyId, bool archived, CancellationToken cancellationToken = default);

    Task<TabMutationResult> SoftDeleteTabAsync(string legacyId, CancellationToken cancellationToken = default);

    /// <summary>Brings a soft-deleted tab back on the board (sets <c>IsDeleted</c> to 0).</summary>
    Task<TabMutationResult> RestoreSoftDeletedTabAsync(string legacyId, CancellationToken cancellationToken = default);

    Task<TabMutationResult> PermanentDeleteTabAsync(string legacyId, CancellationToken cancellationToken = default);

    Task<int> CountArchivedTabsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArchivedTabListItem>> GetArchivedTabsPageAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GuestCloseoutRow>> GetOpenGuestTabsForCloseoutAsync(CancellationToken cancellationToken = default);

    Task<TabMutationResult> CloseGuestTabsEndOfNightAsync(
        IReadOnlyList<string> legacyIds,
        long? closedByBartenderPk,
        string closeReason,
        CancellationToken cancellationToken = default);

    Task<TabMutationResult> CloseAllZeroBalanceGuestTabsAsync(long? closedByBartenderPk, string closeReason, CancellationToken cancellationToken = default);

    Task<TabMutationResult> ArchiveGuestTabsAsync(IReadOnlyList<string> legacyIds, CancellationToken cancellationToken = default);

    Task<TabMutationResult> ArchiveAllOpenGuestTabsAsync(CancellationToken cancellationToken = default);
}
