using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services;

public sealed class SqliteAuthenticationService : IAuthenticationService
{
    private readonly IStaffPinLookupCache _pinCache;
    private readonly ILogger<SqliteAuthenticationService> _logger;

    public SqliteAuthenticationService(IStaffPinLookupCache pinCache, ILogger<SqliteAuthenticationService> logger)
    {
        _pinCache = pinCache;
        _logger = logger;
    }

    public Task<AuthenticationResult> AuthenticateByPinAsync(string pin, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Task.FromResult(_pinCache.Authenticate(pin ?? string.Empty));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PIN authentication failed.");
            return Task.FromResult(
                AuthenticationResult.Fail("Sign-in is unavailable right now. Check the database, then try again."));
        }
    }

    public static bool RequiresPinChange(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.TryGetProperty("requiresPinChange", out var flag)
                && flag.ValueKind == JsonValueKind.True)
            {
                return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }
}

public sealed class StaffPinLookupCache : IStaffPinLookupCache
{
    private readonly SqliteConnectionFactory _factory;
    private readonly ILogger<StaffPinLookupCache> _logger;
    private readonly object _gate = new();
    private Dictionary<string, List<StaffAuthEntry>> _byPlainPin = new(StringComparer.Ordinal);
    private List<StaffAuthEntry> _hashOnly = [];

    public StaffPinLookupCache(SqliteConnectionFactory factory, ILogger<StaffPinLookupCache> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public void Refresh(CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = _factory.OpenConnection();
            var rows = conn.Query<StaffAuthRow>(
                new CommandDefinition(
                    """
                    SELECT Id, LegacyId, Name, PinHash, PinSalt, Role, UiTheme, RawJson, LegacyPinPlain, IsDeveloper
                    FROM Bartenders
                    WHERE IsActive = 1
                    """,
                    cancellationToken: cancellationToken));

            var byPlain = new Dictionary<string, List<StaffAuthEntry>>(StringComparer.Ordinal);
            var hashOnly = new List<StaffAuthEntry>();
            foreach (var r in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = ToEntry(r);
                var plain = StaffPinPinLookupHelper.NormalizePlainPin(r.LegacyPinPlain)
                    ?? StaffPinPinLookupHelper.TryExtractLegacyPlainPin(r.RawJson);
                if (plain is not null)
                {
                    if (!byPlain.TryGetValue(plain, out var list))
                    {
                        list = [];
                        byPlain[plain] = list;
                    }

                    list.Add(entry);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(r.PinHash) && !string.IsNullOrWhiteSpace(r.PinSalt))
                {
                    hashOnly.Add(entry);
                }
            }

            lock (_gate)
            {
                _byPlainPin = byPlain;
                _hashOnly = hashOnly;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild staff PIN lookup cache.");
            lock (_gate)
            {
                _byPlainPin = new Dictionary<string, List<StaffAuthEntry>>(StringComparer.Ordinal);
                _hashOnly = [];
            }
        }
    }

    public AuthenticationResult Authenticate(string pin)
    {
        if (!PosBarPinSecurity.IsValidPinFormat(pin))
        {
            return AuthenticationResult.Fail("Enter a 4-digit PIN.");
        }

        var trimmed = pin.Trim();
        Dictionary<string, List<StaffAuthEntry>> byPlain;
        List<StaffAuthEntry> hashOnly;
        lock (_gate)
        {
            byPlain = _byPlainPin;
            hashOnly = _hashOnly;
        }

        if (byPlain.TryGetValue(trimmed, out var plainMatches))
        {
            return ResultFromMatches(plainMatches);
        }

        var hashMatches = new List<StaffAuthEntry>();
        foreach (var entry in hashOnly)
        {
            if (PosBarPinSecurity.Verify(trimmed, entry.PinHash, entry.PinSalt))
            {
                hashMatches.Add(entry);
            }
        }

        return ResultFromMatches(hashMatches);
    }

    private static AuthenticationResult ResultFromMatches(List<StaffAuthEntry> matches)
    {
        if (matches.Count == 0)
        {
            return AuthenticationResult.Fail("PIN not recognized.");
        }

        if (matches.Count > 1)
        {
            return AuthenticationResult.Fail("PIN matches more than one user. Change duplicate PINs in Admin.");
        }

        var b = matches[0];
        return AuthenticationResult.Success(
            b.Id,
            b.LegacyId,
            string.IsNullOrWhiteSpace(b.Name) ? "Staff" : b.Name,
            b.Role,
            b.UiTheme,
            SqliteAuthenticationService.RequiresPinChange(b.RawJson),
            b.IsDeveloper);
    }

    private static StaffAuthEntry ToEntry(StaffAuthRow r) =>
        new(
            r.Id,
            r.LegacyId,
            r.Name,
            r.PinHash,
            r.PinSalt,
            r.Role,
            r.UiTheme,
            r.RawJson,
            r.IsDeveloper != 0);

    private sealed class StaffAuthRow
    {
        public long Id { get; set; }

        public string? LegacyId { get; set; }

        public string? Name { get; set; }

        public string? PinHash { get; set; }

        public string? PinSalt { get; set; }

        public string? Role { get; set; }

        public string? UiTheme { get; set; }

        public string? RawJson { get; set; }

        public string? LegacyPinPlain { get; set; }

        public long IsDeveloper { get; set; }
    }

    private sealed record StaffAuthEntry(
        long Id,
        string? LegacyId,
        string? Name,
        string? PinHash,
        string? PinSalt,
        string? Role,
        string? UiTheme,
        string? RawJson,
        bool IsDeveloper);
}

internal static class StaffPinPinLookupHelper
{
    public static string? NormalizePlainPin(string? pin)
    {
        var plain = (pin ?? string.Empty).Trim();
        return PosBarPinSecurity.IsValidPinFormat(plain) ? plain : null;
    }

    public static string? TryExtractLegacyPlainPin(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            if (!TryGetPinProperty(root, out var pinElement))
            {
                return null;
            }

            return pinElement.ValueKind switch
            {
                JsonValueKind.String => NormalizePlainPin(pinElement.GetString()),
                JsonValueKind.Number when pinElement.TryGetInt32(out var n) => NormalizePlainPin(n.ToString(CultureInfo.InvariantCulture)),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetPinProperty(JsonElement root, out JsonElement pinElement)
    {
        foreach (var name in new[] { "Pin", "pin", "PIN" })
        {
            if (root.TryGetProperty(name, out pinElement))
            {
                return true;
            }
        }

        pinElement = default;
        return false;
    }
}
