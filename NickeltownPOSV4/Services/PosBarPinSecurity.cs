using System;
using System.Linq;
using System.Security.Cryptography;

namespace NickeltownPOSV4.Services;

/// <summary>PBKDF2 PIN storage compatible with Nickeltown JSON imports (current + legacy iteration/hash variants).</summary>
public static class PosBarPinSecurity
{
    private const int SaltSizeBytes = 16;

    private const int HashSizeBytes = 32;

    private const int Iterations = 100_000;

    /// <summary>PBKDF2 variants tried when verifying imported hashes (older builds used fewer iterations and/or SHA-1).</summary>
    private static readonly (int Iterations, HashAlgorithmName Hash)[] VerifyCandidates =
    [
        (100_000, HashAlgorithmName.SHA256),
        (10_000, HashAlgorithmName.SHA256),
        (5_000, HashAlgorithmName.SHA256),
        (1_000, HashAlgorithmName.SHA256),
        (100_000, HashAlgorithmName.SHA1),
        (10_000, HashAlgorithmName.SHA1),
        (1_000, HashAlgorithmName.SHA1),
    ];

    public static bool IsValidPinFormat(string? pin)
    {
        return !string.IsNullOrWhiteSpace(pin)
            && pin!.Length == 4
            && pin.All(char.IsDigit);
    }

    public static (string HashBase64, string SaltBase64) CreateHash(string pin)
    {
        if (!IsValidPinFormat(pin))
        {
            throw new ArgumentException("PIN must be exactly 4 digits.", nameof(pin));
        }

        var salt = new byte[SaltSizeBytes];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        byte[] hash;
        using (var pbkdf2 = new Rfc2898DeriveBytes(pin, salt, Iterations, HashAlgorithmName.SHA256))
        {
            hash = pbkdf2.GetBytes(HashSizeBytes);
        }

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool Verify(string pin, string? storedHashBase64, string? storedSaltBase64)
    {
        if (string.IsNullOrWhiteSpace(pin)
            || string.IsNullOrWhiteSpace(storedHashBase64)
            || string.IsNullOrWhiteSpace(storedSaltBase64))
        {
            return false;
        }

        if (!TryDecodeBytesFlexible(storedSaltBase64, out var salt)
            || !TryDecodeBytesFlexible(storedHashBase64, out var expectedHash))
        {
            return false;
        }

        foreach (var (iterations, hashAlg) in VerifyCandidates)
        {
            if (TryPbkdf2Verify(pin, salt, expectedHash, iterations, hashAlg))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Verifies a PIN against stored credentials (PBKDF2 hash+salt, plaintext hash column, or legacy plaintext pin field).
    /// Matches POSBar V2 <c>Bartender.VerifyPin</c> behaviour for migrated JSON.
    /// </summary>
    public static bool VerifyStoredCredentials(
        string pin,
        string? storedHashBase64,
        string? storedSaltBase64,
        string? legacyPlainPin = null)
    {
        var entered = (pin ?? string.Empty).Trim();
        if (!IsValidPinFormat(entered))
        {
            return false;
        }

        var plain = (legacyPlainPin ?? string.Empty).Trim();
        if (IsValidPinFormat(plain))
        {
            return string.Equals(plain, entered, StringComparison.Ordinal);
        }

        var hash = (storedHashBase64 ?? string.Empty).Trim();
        var salt = (storedSaltBase64 ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(hash) && !string.IsNullOrEmpty(salt))
        {
            if (Verify(entered, hash, salt))
            {
                return true;
            }
        }

        if (!string.IsNullOrEmpty(hash) && IsValidPinFormat(hash))
        {
            return string.Equals(hash, entered, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool TryPbkdf2Verify(
        string pin,
        byte[] salt,
        byte[] expectedHash,
        int iterations,
        HashAlgorithmName hashAlgorithmName)
    {
        try
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(pin, salt, iterations, hashAlgorithmName);
            var actualHash = pbkdf2.GetBytes(expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Accepts standard base64 or hex (with optional 0x prefix) for legacy exports.</summary>
    private static bool TryDecodeBytesFlexible(string? text, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var s = text.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            s = s[2..];
        }

        try
        {
            bytes = Convert.FromBase64String(s);
            return bytes.Length > 0;
        }
        catch
        {
            // fall through
        }

        try
        {
            var urlSafe = s.Replace('-', '+').Replace('_', '/');
            if (!string.Equals(urlSafe, s, StringComparison.Ordinal))
            {
                bytes = Convert.FromBase64String(urlSafe);
                return bytes.Length > 0;
            }
        }
        catch
        {
            // fall through
        }

        try
        {
            if ((s.Length & 1) == 1)
            {
                return false;
            }

            bytes = Convert.FromHexString(s);
            return bytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
