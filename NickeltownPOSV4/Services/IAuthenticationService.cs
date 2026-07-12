using System.Threading;

using System.Threading.Tasks;



namespace NickeltownPOSV4.Services;



public sealed class AuthenticationResult

{

    public bool Ok { get; init; }



    public string? ErrorMessage { get; init; }



    public long StaffPk { get; init; }



    public string? LegacyId { get; init; }



    public string DisplayName { get; init; } = string.Empty;



    public string? Role { get; init; }



    public string? UiTheme { get; init; }



    public bool RequiresPinChange { get; init; }

    public bool IsDeveloper { get; init; }



    public static AuthenticationResult Fail(string message) =>

        new() { Ok = false, ErrorMessage = message };



    public static AuthenticationResult Success(

        long pk,

        string? legacyId,

        string name,

        string? role,

        string? uiTheme = null,

        bool requiresPinChange = false,

        bool isDeveloper = false) =>

        new()

        {

            Ok = true,

            StaffPk = pk,

            LegacyId = legacyId,

            DisplayName = name,

            Role = role,

            UiTheme = uiTheme,

            RequiresPinChange = requiresPinChange,

            IsDeveloper = isDeveloper,

        };

}



public interface IAuthenticationService
{
    Task<AuthenticationResult> AuthenticateByPinAsync(string pin, CancellationToken cancellationToken = default);
}

/// <summary>In-memory staff PIN index for instant login (rebuilt after bootstrap, import, or staff PIN changes).</summary>
public interface IStaffPinLookupCache
{
    void Refresh(CancellationToken cancellationToken = default);

    AuthenticationResult Authenticate(string pin);
}

