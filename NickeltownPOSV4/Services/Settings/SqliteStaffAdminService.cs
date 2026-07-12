using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Settings;

public sealed class SqliteStaffAdminService : IStaffAdminService
{
    private readonly SqliteConnectionFactory _factory;
    private readonly IStaffPinLookupCache _pinCache;

    public SqliteStaffAdminService(SqliteConnectionFactory factory, IStaffPinLookupCache pinCache)
    {
        _factory = factory;
        _pinCache = pinCache;
    }

    public Task<IReadOnlyList<StaffAdminRow>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<StaffAdminRow>>(
            () =>
            {
                using var conn = _factory.OpenConnection();
                var rows = conn.Query<StaffRow>(
                    """
                    SELECT Id, LegacyId, Name, Role, IsActive, IsDeveloper
                    FROM Bartenders
                    ORDER BY IsActive DESC, Name COLLATE NOCASE ASC
                    """)
                    .ToList();

                return rows
                    .Select(r => new StaffAdminRow(
                        r.Id,
                        r.LegacyId,
                        string.IsNullOrWhiteSpace(r.Name) ? $"Staff {r.Id}" : r.Name!,
                        NormalizeRoleForDisplay(r.Role),
                        r.IsActive != 0,
                        r.IsDeveloper != 0))
                    .ToList();
            },
            cancellationToken);

    public Task<long> CreateAsync(string displayName, string role, string pin4, CancellationToken cancellationToken = default) =>
        Task.Run(
            () =>
            {
                var name = (displayName ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentException("Display name is required.", nameof(displayName));
                }

                if (!PosBarPinSecurity.IsValidPinFormat(pin4))
                {
                    throw new ArgumentException("PIN must be exactly 4 digits.", nameof(pin4));
                }

                if (FindActiveStaffByPin(pin4, excludeId: null) is long existingMatchId)
                {
                    throw new InvalidOperationException(
                        $"That PIN is already in use by another active user (ID {existingMatchId}). Pick a different 4-digit PIN.");
                }

                var (hash, salt) = PosBarPinSecurity.CreateHash(pin4);
                var legacyId = "staff-" + Guid.NewGuid().ToString("n");

                using var conn = _factory.OpenConnection();
                conn.Execute(
                    """
                    INSERT INTO Bartenders (LegacyId, LegacyKey, Name, PinHash, PinSalt, Role, IsActive, RawJson, CreatedAt, UpdatedAt)
                    VALUES (@LegacyId, @LegacyKey, @Name, @PinHash, @PinSalt, @Role, 1, NULL, datetime('now'), datetime('now'))
                    """,
                    new
                    {
                        LegacyId = legacyId,
                        LegacyKey = legacyId,
                        Name = name,
                        PinHash = hash,
                        PinSalt = salt,
                        Role = NormalizeRoleForStorage(role),
                    });

                var id = conn.ExecuteScalar<long>("SELECT last_insert_rowid();");
                _pinCache.Refresh(cancellationToken);
                return id;
            },
            cancellationToken);

    public Task UpdateAsync(long id, string displayName, string role, bool isActive, bool isDeveloper, CancellationToken cancellationToken = default) =>
        Task.Run(
            () =>
            {
                var name = (displayName ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentException("Display name is required.", nameof(displayName));
                }

                using var conn = _factory.OpenConnection();
                conn.Execute(
                    """
                    UPDATE Bartenders
                    SET Name = @Name,
                        Role = @Role,
                        IsActive = @IsActive,
                        IsDeveloper = @IsDeveloper,
                        UpdatedAt = datetime('now')
                    WHERE Id = @Id
                    """,
                    new
                    {
                        Id = id,
                        Name = name,
                        Role = NormalizeRoleForStorage(role),
                        IsActive = isActive ? 1 : 0,
                        IsDeveloper = isDeveloper ? 1 : 0,
                    });
                _pinCache.Refresh(cancellationToken);
            },
            cancellationToken);

    public Task ResetPinAsync(long id, string newPin4, CancellationToken cancellationToken = default) =>
        Task.Run(
            () => ApplyPinUpdate(id, newPin4, clearRequiresPinChange: false),
            cancellationToken);

    public Task CompleteForcedPinChangeAsync(long id, string newPin4, CancellationToken cancellationToken = default) =>
        Task.Run(
            () => ApplyPinUpdate(id, newPin4, clearRequiresPinChange: true),
            cancellationToken);

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        Task.Run(
            () =>
            {
                using var conn = _factory.OpenConnection();
                // Soft-deactivate by default to preserve foreign-key history; admin can hard delete via direct DB if needed.
                conn.Execute(
                    "UPDATE Bartenders SET IsActive = 0, UpdatedAt = datetime('now') WHERE Id = @Id",
                    new { Id = id });
                _pinCache.Refresh(cancellationToken);
            },
            cancellationToken);

    /// <summary>Walks all active staff PIN hashes and returns the first ID that verifies against <paramref name="pin4"/>, or null if none collide.</summary>
    private void ApplyPinUpdate(long id, string newPin4, bool clearRequiresPinChange)
    {
        if (!PosBarPinSecurity.IsValidPinFormat(newPin4))
        {
            throw new ArgumentException("PIN must be exactly 4 digits.", nameof(newPin4));
        }

        if (FindActiveStaffByPin(newPin4, excludeId: id) is long collidingId)
        {
            throw new InvalidOperationException(
                $"That PIN is already in use by another active user (ID {collidingId}). Pick a different 4-digit PIN.");
        }

        var (hash, salt) = PosBarPinSecurity.CreateHash(newPin4);
        using var conn = _factory.OpenConnection();
        if (clearRequiresPinChange)
        {
            var rawJson = conn.QuerySingleOrDefault<string?>(
                "SELECT RawJson FROM Bartenders WHERE Id = @Id",
                new { Id = id });
            var updatedJson = ClearRequiresPinChangeFlag(rawJson);
            conn.Execute(
                """
                UPDATE Bartenders
                SET PinHash = @PinHash,
                    PinSalt = @PinSalt,
                    LegacyPinPlain = NULL,
                    RawJson = @RawJson,
                    UpdatedAt = datetime('now')
                WHERE Id = @Id
                """,
                new { Id = id, PinHash = hash, PinSalt = salt, RawJson = updatedJson });
            _pinCache.Refresh();
            return;
        }

        conn.Execute(
            """
            UPDATE Bartenders
            SET PinHash = @PinHash,
                PinSalt = @PinSalt,
                LegacyPinPlain = NULL,
                UpdatedAt = datetime('now')
            WHERE Id = @Id
            """,
            new { Id = id, PinHash = hash, PinSalt = salt });
        _pinCache.Refresh();
    }

    private static string ClearRequiresPinChangeFlag(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return """{"requiresPinChange":false}""";
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var buffer = new System.Text.Json.Nodes.JsonObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, "requiresPinChange", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                buffer[prop.Name] = System.Text.Json.Nodes.JsonNode.Parse(prop.Value.GetRawText());
            }

            buffer["requiresPinChange"] = false;
            return buffer.ToJsonString();
        }
        catch (JsonException)
        {
            return """{"requiresPinChange":false}""";
        }
    }

    private long? FindActiveStaffByPin(string pin4, long? excludeId)
    {
        var auth = _pinCache.Authenticate(pin4);
        if (!auth.Ok)
        {
            return null;
        }

        if (excludeId.HasValue && auth.StaffPk == excludeId.Value)
        {
            return null;
        }

        return auth.StaffPk;
    }

    private static string NormalizeRoleForDisplay(string? role)
    {
        var r = (role ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(r))
        {
            return "Staff";
        }

        // if (string.Equals(r, "treasurer", StringComparison.OrdinalIgnoreCase))
        // {
        //     return "Treasurer";
        // }

        return string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "Staff";
    }

    private static string NormalizeRoleForStorage(string? role)
    {
        var r = (role ?? string.Empty).Trim();
        // if (string.Equals(r, "treasurer", StringComparison.OrdinalIgnoreCase))
        // {
        //     return "treasurer";
        // }

        return string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase) ? "admin" : "staff";
    }

    private sealed class StaffRow
    {
        public long Id { get; set; }

        public string? LegacyId { get; set; }

        public string? Name { get; set; }

        public string? Role { get; set; }

        public long IsActive { get; set; }

        public long IsDeveloper { get; set; }
    }
}
