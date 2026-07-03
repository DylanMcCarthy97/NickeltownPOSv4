using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteMembershipFormContentRepository : IMembershipFormContentRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteMembershipFormContentRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task<IReadOnlyList<MembershipFormContentSection>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<MembershipFormContentDbRow>(
            new CommandDefinition(
                """
                SELECT Id, SectionKey, Title, Body, SortOrder, UpdatedAt
                FROM MembershipFormContent
                ORDER BY SortOrder, Id
                """,
                cancellationToken: cancellationToken));

        return Task.FromResult<IReadOnlyList<MembershipFormContentSection>>(rows.Select(MapRow).ToList());
    }

    public Task<MembershipFormContentSection?> GetByKeyAsync(string sectionKey, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var row = conn.QueryFirstOrDefault<MembershipFormContentDbRow>(
            new CommandDefinition(
                """
                SELECT Id, SectionKey, Title, Body, SortOrder, UpdatedAt
                FROM MembershipFormContent
                WHERE SectionKey = @sectionKey
                """,
                new { sectionKey },
                cancellationToken: cancellationToken));

        return Task.FromResult(row is null ? null : MapRow(row));
    }

    private static MembershipFormContentSection MapRow(MembershipFormContentDbRow row) =>
        new()
        {
            Id = row.Id,
            SectionKey = row.SectionKey,
            Title = row.Title,
            Body = row.Body,
            SortOrder = row.SortOrder,
            UpdatedAt = ParseOffset(row.UpdatedAt),
        };

    private static DateTimeOffset ParseOffset(string? value) =>
        string.IsNullOrWhiteSpace(value) || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
            ? DateTimeOffset.UtcNow
            : dto;

    private sealed class MembershipFormContentDbRow
    {
        public long Id { get; init; }

        public string SectionKey { get; init; } = string.Empty;

        public string? Title { get; init; }

        public string Body { get; init; } = string.Empty;

        public int SortOrder { get; init; }

        public string? UpdatedAt { get; init; }
    }
}
