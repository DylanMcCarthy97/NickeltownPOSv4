using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Models.Audit;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteAuditLogRepository : IAuditLogRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteAuditLogRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task<long> InsertAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var occurredAt = (entry.OccurredAt == default ? DateTimeOffset.UtcNow : entry.OccurredAt)
            .UtcDateTime
            .ToString("O", CultureInfo.InvariantCulture);

        var id = conn.ExecuteScalar<long>(
            new CommandDefinition(
                """
                INSERT INTO AuditLog (
                  OccurredAt, StaffId, StaffName, StaffRole,
                  ActionType, EntityType, EntityId,
                  Amount, Reason, Success, DetailsJson, CreatedAt)
                VALUES (
                  @OccurredAt, @StaffId, @StaffName, @StaffRole,
                  @ActionType, @EntityType, @EntityId,
                  @Amount, @Reason, @Success, @DetailsJson, datetime('now'));
                SELECT last_insert_rowid();
                """,
                new
                {
                    OccurredAt = occurredAt,
                    entry.StaffId,
                    StaffName = string.IsNullOrWhiteSpace(entry.StaffName) ? null : entry.StaffName.Trim(),
                    StaffRole = string.IsNullOrWhiteSpace(entry.StaffRole) ? null : entry.StaffRole.Trim(),
                    ActionType = entry.ActionType.Trim(),
                    EntityType = string.IsNullOrWhiteSpace(entry.EntityType) ? null : entry.EntityType.Trim(),
                    EntityId = string.IsNullOrWhiteSpace(entry.EntityId) ? null : entry.EntityId.Trim(),
                    Amount = entry.Amount,
                    Reason = string.IsNullOrWhiteSpace(entry.Reason) ? null : entry.Reason.Trim(),
                    Success = entry.Success ? 1 : 0,
                    entry.DetailsJson,
                },
                cancellationToken: cancellationToken));

        return Task.FromResult(id);
    }

    public Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int maxEntries, CancellationToken cancellationToken = default)
    {
        var limit = maxEntries <= 0 ? 200 : Math.Min(maxEntries, 2000);
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<AuditLogDbRow>(
            new CommandDefinition(
                """
                SELECT Id, OccurredAt, StaffId, StaffName, StaffRole,
                       ActionType, EntityType, EntityId,
                       Amount, Reason, Success, DetailsJson
                FROM AuditLog
                ORDER BY datetime(OccurredAt) DESC, Id DESC
                LIMIT @limit
                """,
                new { limit },
                cancellationToken: cancellationToken));

        var list = rows.Select(MapRow).ToList();
        return Task.FromResult<IReadOnlyList<AuditLogEntry>>(list);
    }

    public Task<IReadOnlyList<AuditLogEntry>> GetForEntityAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<AuditLogDbRow>(
            new CommandDefinition(
                """
                SELECT Id, OccurredAt, StaffId, StaffName, StaffRole,
                       ActionType, EntityType, EntityId,
                       Amount, Reason, Success, DetailsJson
                FROM AuditLog
                WHERE EntityType = @entityType AND EntityId = @entityId
                ORDER BY datetime(OccurredAt) DESC, Id DESC
                """,
                new { entityType, entityId },
                cancellationToken: cancellationToken));

        var list = rows.Select(MapRow).ToList();
        return Task.FromResult<IReadOnlyList<AuditLogEntry>>(list);
    }

    private static AuditLogEntry MapRow(AuditLogDbRow r) =>
        new()
        {
            Id = r.Id,
            OccurredAt = ParseOffset(r.OccurredAt),
            StaffId = r.StaffId,
            StaffName = r.StaffName,
            StaffRole = r.StaffRole,
            ActionType = r.ActionType,
            EntityType = r.EntityType,
            EntityId = r.EntityId,
            Amount = r.Amount,
            Reason = r.Reason,
            Success = r.Success != 0,
            DetailsJson = r.DetailsJson,
        };

    private static DateTimeOffset ParseOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTimeOffset.MinValue;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            return dto;
        }

        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }

    private sealed class AuditLogDbRow
    {
        public long Id { get; init; }

        public string OccurredAt { get; init; } = string.Empty;

        public long? StaffId { get; init; }

        public string? StaffName { get; init; }

        public string? StaffRole { get; init; }

        public string ActionType { get; init; } = string.Empty;

        public string? EntityType { get; init; }

        public string? EntityId { get; init; }

        public decimal? Amount { get; init; }

        public string? Reason { get; init; }

        public int Success { get; init; }

        public string? DetailsJson { get; init; }
    }
}
