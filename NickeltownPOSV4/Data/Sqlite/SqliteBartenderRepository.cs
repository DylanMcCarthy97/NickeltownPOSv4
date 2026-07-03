using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Data.Migration;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteBartenderRepository : IBartenderMigrationRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteBartenderRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task ImportBartendersAsync(IReadOnlyList<LegacyBartenderDto> bartenders, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();
        foreach (var dto in bartenders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var legacyId = string.IsNullOrWhiteSpace(dto.Id) ? LegacyStableId.ForBartender(dto) : dto.Id!;
            var raw = JsonSerializer.Serialize(dto);
            var legacyPinPlain = StaffPinPinLookupHelper.NormalizePlainPin(dto.Pin);
            string pinHashCol;
            string? pinSaltCol;
            if (string.IsNullOrWhiteSpace(dto.PinHash)
                && legacyPinPlain is not null)
            {
                var migrated = PosBarPinSecurity.CreateHash(legacyPinPlain);
                pinHashCol = migrated.HashBase64;
                pinSaltCol = migrated.SaltBase64;
            }
            else
            {
                pinHashCol = !string.IsNullOrWhiteSpace(dto.PinHash) ? dto.PinHash : dto.Pin ?? string.Empty;
                pinSaltCol = string.IsNullOrWhiteSpace(dto.PinSalt) ? null : dto.PinSalt;
            }
            conn.Execute(
                """
                INSERT INTO Bartenders (LegacyId, LegacyKey, Name, PinHash, PinSalt, LegacyPinPlain, Role, IsActive, RawJson, CreatedAt, UpdatedAt)
                VALUES (@LegacyId, @LegacyKey, @Name, @PinHash, @PinSalt, @LegacyPinPlain, @Role, @IsActive, @RawJson, datetime('now'), datetime('now'))
                ON CONFLICT(LegacyId) DO UPDATE SET
                  Name = excluded.Name,
                  PinHash = excluded.PinHash,
                  PinSalt = excluded.PinSalt,
                  LegacyPinPlain = excluded.LegacyPinPlain,
                  Role = excluded.Role,
                  IsActive = excluded.IsActive,
                  RawJson = excluded.RawJson,
                  UpdatedAt = datetime('now')
                """,
                new
                {
                    LegacyId = legacyId,
                    LegacyKey = legacyId,
                    Name = dto.Name ?? "Bartender",
                    PinHash = pinHashCol,
                    PinSalt = pinSaltCol,
                    LegacyPinPlain = legacyPinPlain,
                    Role = dto.Role,
                    IsActive = dto.Active != false ? 1 : 0,
                    RawJson = raw,
                },
                tx);
        }

        tx.Commit();
        return Task.CompletedTask;
    }
}
