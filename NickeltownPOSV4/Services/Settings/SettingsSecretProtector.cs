using System;
using System.Security.Cryptography;
using System.Text;

namespace NickeltownPOSV4.Services.Settings;

/// <summary>Protects settings values at rest using Windows DPAPI (current user scope).</summary>
public static class SettingsSecretProtector
{
    private const string Prefix = "dpapi1:";

    public static bool IsProtectedPayload(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Protect(string plaintext)
    {
        if (plaintext is null)
        {
            throw new ArgumentNullException(nameof(plaintext));
        }

        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    public static bool TryUnprotect(string? stored, out string plaintext)
    {
        plaintext = string.Empty;
        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        if (!IsProtectedPayload(stored))
        {
            plaintext = stored;
            return true;
        }

        try
        {
            var b64 = stored[Prefix.Length..];
            var protectedBytes = Convert.FromBase64String(b64);
            var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            plaintext = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
