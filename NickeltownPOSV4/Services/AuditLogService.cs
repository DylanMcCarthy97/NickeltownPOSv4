using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Audit;

namespace NickeltownPOSV4.Services;

public sealed class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _repo;
    private readonly IUserSessionService _session;

    public AuditLogService(IAuditLogRepository repo, IUserSessionService session)
    {
        _repo = repo;
        _session = session;
    }

    public Task<long> LogAsync(AuditLogEntryRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Task.FromResult(0L);
        }

        return LogInternalAsync(request, cancellationToken);
    }

    public Task<long> LogAsync(
        string actionType,
        string? entityType = null,
        string? entityId = null,
        decimal? amount = null,
        string? reason = null,
        bool success = true,
        string? detailsJson = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            return Task.FromResult(0L);
        }

        var req = new AuditLogEntryRequest
        {
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            Amount = amount,
            Reason = reason,
            Success = success,
            DetailsJson = detailsJson,
        };

        return LogInternalAsync(req, cancellationToken);
    }

    private async Task<long> LogInternalAsync(AuditLogEntryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var entry = new AuditLogEntry
            {
                OccurredAt = DateTimeOffset.UtcNow,
                StaffId = _session.ActiveStaffId,
                StaffName = _session.DisplayName,
                StaffRole = _session.Role,
                ActionType = request.ActionType,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                Amount = request.Amount,
                Reason = request.Reason,
                Success = request.Success,
                DetailsJson = request.DetailsJson,
            };

            return await _repo.InsertAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Audit logging must never break the calling flow.
            Debug.WriteLine($"[AuditLogService] Failed to log {request.ActionType}: {ex.Message}");
            return 0L;
        }
    }
}
