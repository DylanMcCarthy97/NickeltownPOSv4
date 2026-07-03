namespace NickeltownPOSV4.Services;

/// <summary>Clears the signed-in staff session and returns to the login screen.</summary>
public interface IAuthSignOutService
{
    /// <summary>Manual sign-out (optional tab undo reset).</summary>
    void SignOut(bool clearTabUndo = false);

    /// <summary>Idle timeout sign-out - auth session only; operational data is preserved.</summary>
    void SignOutForInactivity();
}