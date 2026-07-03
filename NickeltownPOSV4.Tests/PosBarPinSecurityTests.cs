using NickeltownPOSV4.Services;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class PosBarPinSecurityTests
{
    [Fact]
    public void CreateHash_ThenVerify_Succeeds()
    {
        var (hash, salt) = PosBarPinSecurity.CreateHash("5678");
        Assert.True(PosBarPinSecurity.Verify("5678", hash, salt));
        Assert.False(PosBarPinSecurity.Verify("1234", hash, salt));
    }

    [Fact]
    public void VerifyStoredCredentials_LegacyPlainPinCheckedBeforePbkdf2()
    {
        var (hash, salt) = PosBarPinSecurity.CreateHash("9999");
        Assert.True(PosBarPinSecurity.VerifyStoredCredentials("1234", hash, salt, legacyPlainPin: "1234"));
        Assert.False(PosBarPinSecurity.VerifyStoredCredentials("1234", hash, salt, legacyPlainPin: "5678"));
    }

    [Fact]
    public void RequiresPinChange_ReadsBootstrapJson()
    {
        const string json = "{\"seed\":true,\"requiresPinChange\":true}";
        Assert.True(SqliteAuthenticationService.RequiresPinChange(json));
        Assert.False(SqliteAuthenticationService.RequiresPinChange("{\"requiresPinChange\":false}"));
    }
}