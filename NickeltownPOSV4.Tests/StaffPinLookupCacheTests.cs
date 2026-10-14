using System.Diagnostics;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using Xunit;

namespace NickeltownPOSV4.Tests;

[Collection("LoginTransitionProbe")]
public sealed class StaffPinLookupCacheTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;
    private readonly StaffPinLookupCache _cache;
    private int _legacySeq;

    public StaffPinLookupCacheTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"ntpos_pincache_{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_dbPath);
        new DatabaseInitializer(_factory).InitializeAsync().GetAwaiter().GetResult();
        _cache = new StaffPinLookupCache(_factory, NullLogger<StaffPinLookupCache>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // ignore temp cleanup
        }
    }

    [Fact]
    public void HashedPin_SecondAuthenticate_IsCachedAndFast()
    {
        SeedHashed("Dylan", "2468", "admin");
        _cache.Refresh();

        var first = _cache.Authenticate("2468");
        Assert.True(first.Ok, first.ErrorMessage);
        Assert.Equal("Dylan", first.DisplayName);
        Assert.Equal("admin", first.Role);

        var sw = Stopwatch.StartNew();
        var second = _cache.Authenticate("2468");
        sw.Stop();

        Assert.True(second.Ok, second.ErrorMessage);
        Assert.Equal(first.StaffPk, second.StaffPk);
        Assert.True(
            sw.ElapsedMilliseconds < 50,
            $"Cached hashed PIN lookup took {sw.ElapsedMilliseconds}ms; first verify should have been memoized.");
    }

    [Fact]
    public void DuplicateHashedPins_AreRejected_AndStayRejected()
    {
        SeedHashed("Ann", "1111", "staff");
        SeedHashed("Bob", "1111", "staff");
        _cache.Refresh();

        var first = _cache.Authenticate("1111");
        Assert.False(first.Ok);
        Assert.Contains("more than one", first.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var second = _cache.Authenticate("1111");
        Assert.False(second.Ok);
        Assert.Contains("more than one", second.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WrongHashedPin_IsRejected()
    {
        SeedHashed("Dylan", "2468", "admin");
        _cache.Refresh();

        var result = _cache.Authenticate("9999");
        Assert.False(result.Ok);
        Assert.Equal("PIN not recognized.", result.ErrorMessage);
    }

    [Fact]
    public void Refresh_AfterPinChange_RejectsOldPin()
    {
        var staffId = SeedHashed("Dylan", "2468", "admin");
        _cache.Refresh();
        Assert.True(_cache.Authenticate("2468").Ok);

        var (hash, salt) = PosBarPinSecurity.CreateHash("1357");
        using (var conn = _factory.OpenConnection())
        {
            conn.Execute(
                "UPDATE Bartenders SET PinHash = @hash, PinSalt = @salt, LegacyPinPlain = NULL WHERE Id = @id",
                new { hash, salt, id = staffId });
        }

        _cache.Refresh();
        Assert.False(_cache.Authenticate("2468").Ok);
        var changed = _cache.Authenticate("1357");
        Assert.True(changed.Ok, changed.ErrorMessage);
        Assert.Equal(staffId, changed.StaffPk);
    }

    [Fact]
    public void PlainPin_IsInstantAndDoesNotGrantAccessOnWrongPin()
    {
        SeedPlain("Ann", "1234", "staff");
        _cache.Refresh();

        var sw = Stopwatch.StartNew();
        var ok = _cache.Authenticate("1234");
        sw.Stop();

        Assert.True(ok.Ok, ok.ErrorMessage);
        Assert.Equal("Ann", ok.DisplayName);
        Assert.True(sw.ElapsedMilliseconds < 20, $"Plain PIN lookup took {sw.ElapsedMilliseconds}ms");
        Assert.False(_cache.Authenticate("0000").Ok);
    }

    [Fact]
    public async Task AuthenticateByPinAsync_Succeeds_ThenSecondCallIsFast()
    {
        SeedHashed("Dylan", "2468", "admin");
        _cache.Refresh();
        var auth = new SqliteAuthenticationService(_cache, NullLogger<SqliteAuthenticationService>.Instance);

        var first = await auth.AuthenticateByPinAsync("2468");
        Assert.True(first.Ok, first.ErrorMessage);

        var sw = Stopwatch.StartNew();
        var second = await auth.AuthenticateByPinAsync("2468");
        sw.Stop();

        Assert.True(second.Ok, second.ErrorMessage);
        Assert.Equal(first.StaffPk, second.StaffPk);
        Assert.True(
            sw.ElapsedMilliseconds < 80,
            $"Second AuthenticateByPinAsync took {sw.ElapsedMilliseconds}ms after memoize.");
    }

    private long SeedHashed(string name, string pin, string role)
    {
        var (hash, salt) = PosBarPinSecurity.CreateHash(pin);
        return InsertBartender(name, role, hash, salt, legacyPinPlain: null);
    }

    private long SeedPlain(string name, string pin, string role)
    {
        var (hash, salt) = PosBarPinSecurity.CreateHash(pin);
        return InsertBartender(name, role, hash, salt, legacyPinPlain: pin);
    }

    private long InsertBartender(string name, string role, string hash, string salt, string? legacyPinPlain)
    {
        _legacySeq++;
        var legacyId = $"test-staff-{_legacySeq}";
        using var conn = _factory.OpenConnection();
        conn.Execute(
            """
            INSERT INTO Bartenders (LegacyId, LegacyKey, Name, PinHash, PinSalt, LegacyPinPlain, Role, IsActive, RawJson, CreatedAt, UpdatedAt)
            VALUES (@legacyId, @legacyId, @name, @hash, @salt, @legacyPinPlain, @role, 1, '{}', datetime('now'), datetime('now'))
            """,
            new { legacyId, name, hash, salt, legacyPinPlain, role });
        return conn.ExecuteScalar<long>("SELECT last_insert_rowid();");
    }
}
