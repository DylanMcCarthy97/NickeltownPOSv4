using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services;

public sealed class DefaultStaffBootstrapper : IDefaultStaffBootstrapper
{
    private const string DefaultLegacyId = "seed-default-admin";

    private readonly SqliteConnectionFactory _factory;

    public DefaultStaffBootstrapper(SqliteConnectionFactory factory) => _factory = factory;

    /// <summary>Seeds a default Admin (PIN 1234) only when the Bartenders table has no rows.</summary>
    public Task EnsureDefaultStaffIfEmptyAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var count = conn.ExecuteScalar<long>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM Bartenders;",
                cancellationToken: cancellationToken));
        if (count > 0)
        {
            return Task.CompletedTask;
        }

        var (hash, salt) = PosBarPinSecurity.CreateHash("1234");
        conn.Execute(
            new CommandDefinition(
                """
                INSERT INTO Bartenders (LegacyId, LegacyKey, Name, PinHash, PinSalt, Role, IsActive, RawJson, CreatedAt, UpdatedAt)
                VALUES (@LegacyId, @LegacyKey, @Name, @PinHash, @PinSalt, @Role, 1, @RawJson, datetime('now'), datetime('now'))
                """,
                new
                {
                    LegacyId = DefaultLegacyId,
                    LegacyKey = DefaultLegacyId,
                    Name = "Admin",
                    PinHash = hash,
                    PinSalt = salt,
                    Role = "admin",
                    RawJson = """{"seed":true,"requiresPinChange":true,"note":"Default bootstrap; change PIN before use."}""",
                },
                cancellationToken: cancellationToken));

#if DEBUG
        Debug.WriteLine(
            "[NickeltownPOS] Seeded default operator (Admin, admin). DEBUG-only: set PINs in Admin before production.");
#endif

        return Task.CompletedTask;
    }
}
