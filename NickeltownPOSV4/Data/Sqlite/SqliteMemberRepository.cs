using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Data.Migration;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteMemberRepository : IMemberMigrationRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteMemberRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task ImportMembersAsync(IReadOnlyList<LegacyMemberDto> members, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();
        foreach (var dto in members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var legacyId = string.IsNullOrWhiteSpace(dto.Id) ? LegacyStableId.ForMember(dto) : dto.Id!;
            var raw = JsonSerializer.Serialize(dto);
            conn.Execute(
                """
                INSERT INTO Members (LegacyId, LegacyKey, Name, Email, Phone, Balance, RawJson, IsActive, CreatedAt, UpdatedAt)
                VALUES (@LegacyId, @LegacyKey, @Name, @Email, @Phone, @Balance, @RawJson, 1, datetime('now'), datetime('now'))
                ON CONFLICT(LegacyId) DO UPDATE SET
                  Name = excluded.Name,
                  Email = excluded.Email,
                  Phone = excluded.Phone,
                  Balance = excluded.Balance,
                  RawJson = excluded.RawJson,
                  UpdatedAt = datetime('now')
                """,
                new
                {
                    LegacyId = legacyId,
                    LegacyKey = legacyId,
                    Name = dto.Name,
                    Email = dto.Email,
                    Phone = dto.Phone,
                    Balance = dto.Balance ?? 0m,
                    RawJson = raw,
                },
                tx);
        }

        tx.Commit();
        return Task.CompletedTask;
    }
}
