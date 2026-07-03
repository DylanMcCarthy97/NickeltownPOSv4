using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteMemberDirectoryQuery : IMemberDirectoryQuery
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteMemberDirectoryQuery(SqliteConnectionFactory factory) => _factory = factory;

    public Task<IReadOnlyList<MemberPickerRow>> GetActiveMembersAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<SqlRow>(
            new CommandDefinition(
                """
                SELECT LegacyId, Name
                FROM Members
                WHERE COALESCE(IsActive,1) != 0
                ORDER BY COALESCE(Name,''), LegacyId
                """,
                cancellationToken: cancellationToken));

        var list = new List<MemberPickerRow>();
        foreach (var r in rows)
        {
            var id = (r.LegacyId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var name = (r.Name ?? string.Empty).Trim();
            var label = string.IsNullOrEmpty(name) ? id : name;
            list.Add(new MemberPickerRow { LegacyId = id, DisplayName = label });
        }

        return Task.FromResult<IReadOnlyList<MemberPickerRow>>(list);
    }

    private sealed class SqlRow
    {
        public string? LegacyId { get; set; }

        public string? Name { get; set; }
    }
}
