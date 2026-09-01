using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.Tabs;

namespace NickeltownPOSV4.Services.Complimentary;

public interface IComplimentaryItemService
{
    Task<ComplimentaryRecordResult> RecordAsync(
        long itemId,
        int quantity = 1,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<ComplimentaryReverseResult> ReverseAsync(
        string issueGuid,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuickFreeButtonRow>> GetButtonsAsync(CancellationToken cancellationToken = default);

    Task<ComplimentaryReport> GetReportAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuickFreeConfigRow>> GetConfigAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuickFreeProductCandidate>> GetProductCandidatesAsync(CancellationToken cancellationToken = default);

    Task<ComplimentaryConfigResult> AddConfigAsync(
        long itemId,
        string? displayLabel,
        string? icon,
        CancellationToken cancellationToken = default);

    Task<ComplimentaryConfigResult> RemoveConfigAsync(long itemId, CancellationToken cancellationToken = default);

    Task<ComplimentaryConfigResult> UpdateConfigAsync(
        long itemId,
        string? displayLabel,
        string? icon,
        CancellationToken cancellationToken = default);

    Task<ComplimentaryConfigResult> MoveConfigAsync(
        long itemId,
        int direction,
        CancellationToken cancellationToken = default);
}

public sealed class ComplimentaryItemService : IComplimentaryItemService
{
    private readonly IComplimentaryItemRepository _repo;
    private readonly IUserSessionService _session;
    private readonly IBarCatalogCache _barCatalogCache;
    private readonly ITabWorkspaceUndoStack _undo;

    public ComplimentaryItemService(
        IComplimentaryItemRepository repo,
        IUserSessionService session,
        IBarCatalogCache barCatalogCache,
        ITabWorkspaceUndoStack undo)
    {
        _repo = repo;
        _session = session;
        _barCatalogCache = barCatalogCache;
        _undo = undo;
    }

    public async Task<ComplimentaryRecordResult> RecordAsync(
        long itemId,
        int quantity = 1,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _repo.RecordAsync(
            new ComplimentaryRecordRequest
            {
                ItemId = itemId,
                Quantity = quantity,
                StaffId = _session.ActiveStaffId,
                StaffName = _session.DisplayName,
                IdempotencyKey = idempotencyKey,
            },
            cancellationToken).ConfigureAwait(false);

        if (!result.Ok || result.AlreadyRecorded || string.IsNullOrWhiteSpace(result.IssueGuid))
        {
            return result;
        }

        _barCatalogCache.Invalidate();
        var issueGuid = result.IssueGuid;
        var itemName = result.ItemName;
        var qty = result.Quantity;
        _undo.PushUndo(
            TabUndoPreview.ForComplimentaryItem(itemName, qty),
            async () =>
            {
                var rev = await ReverseAsync(issueGuid, CancellationToken.None).ConfigureAwait(false);
                return rev.Ok;
            });

        return result;
    }

    public async Task<ComplimentaryReverseResult> ReverseAsync(
        string issueGuid,
        CancellationToken cancellationToken = default)
    {
        var result = await _repo.ReverseAsync(
            issueGuid,
            _session.ActiveStaffId,
            _session.DisplayName,
            cancellationToken).ConfigureAwait(false);
        if (result.Ok)
        {
            _barCatalogCache.Invalidate();
        }

        return result;
    }

    public Task<IReadOnlyList<QuickFreeButtonRow>> GetButtonsAsync(CancellationToken cancellationToken = default) =>
        _repo.GetButtonsAsync(DateOnly.FromDateTime(DateTime.Now), cancellationToken);

    public Task<ComplimentaryReport> GetReportAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default) =>
        _repo.GetReportAsync(fromInclusive, toInclusive, cancellationToken);

    public Task<IReadOnlyList<QuickFreeConfigRow>> GetConfigAsync(CancellationToken cancellationToken = default) =>
        _repo.GetConfigAsync(cancellationToken);

    public Task<IReadOnlyList<QuickFreeProductCandidate>> GetProductCandidatesAsync(CancellationToken cancellationToken = default) =>
        _repo.GetProductCandidatesAsync(cancellationToken);

    public Task<ComplimentaryConfigResult> AddConfigAsync(
        long itemId,
        string? displayLabel,
        string? icon,
        CancellationToken cancellationToken = default) =>
        _repo.AddConfigAsync(itemId, displayLabel, icon, cancellationToken);

    public Task<ComplimentaryConfigResult> RemoveConfigAsync(long itemId, CancellationToken cancellationToken = default) =>
        _repo.RemoveConfigAsync(itemId, cancellationToken);

    public Task<ComplimentaryConfigResult> UpdateConfigAsync(
        long itemId,
        string? displayLabel,
        string? icon,
        CancellationToken cancellationToken = default) =>
        _repo.UpdateConfigAsync(itemId, displayLabel, icon, cancellationToken);

    public Task<ComplimentaryConfigResult> MoveConfigAsync(
        long itemId,
        int direction,
        CancellationToken cancellationToken = default) =>
        _repo.MoveConfigAsync(itemId, direction, cancellationToken);
}
