namespace NickeltownPOSV4.Services;

public sealed class AuthSignOutService : IAuthSignOutService
{
    private readonly IUserSessionService _session;
    private readonly IRootNavigationCoordinator _rootNav;
    private readonly ISlidePanelService _slide;
    private readonly ITabWorkspaceUndoStack _undo;

    public AuthSignOutService(
        IUserSessionService session,
        IRootNavigationCoordinator rootNav,
        ISlidePanelService slide,
        ITabWorkspaceUndoStack undo)
    {
        _session = session;
        _rootNav = rootNav;
        _slide = slide;
        _undo = undo;
    }

    public void SignOut(bool clearTabUndo = false)
    {
        if (!_session.IsSignedIn)
        {
            return;
        }

        if (clearTabUndo)
        {
            _undo.Clear();
        }

        SignOutForInactivity();
    }

    public void SignOutForInactivity()
    {
        if (!_session.IsSignedIn)
        {
            return;
        }

        _slide.Close();
        _session.Clear();
        _rootNav.NavigateToLogin();
    }
}