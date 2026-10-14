using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.Services.Logging;
using NickeltownPOSV4.Themes;

namespace NickeltownPOSV4.Services;

public sealed class RootNavigationCoordinator : IRootNavigationCoordinator
{
    private readonly IPosThemeService _themes;

    private readonly IUserSessionService _session;

    private Frame? _rootFrame;

    public RootNavigationCoordinator(IPosThemeService themes, IUserSessionService session)
    {
        _themes = themes;
        _session = session;
    }

    public void Attach(Frame rootFrame) => _rootFrame = rootFrame;

    public void NavigateToStartup()
    {
        if (_rootFrame is null)
        {
            return;
        }

        _themes.Apply(UiThemeId.Light);
        _ = _rootFrame.Navigate(typeof(Views.StartupPage));
    }

    public void NavigateToLogin()
    {
        if (_rootFrame is null)
        {
            return;
        }

        _themes.Apply(UiThemeId.Light);
        _ = _rootFrame.Navigate(typeof(Views.LoginPage));
    }

    public void NavigateToForcedPinChange()
    {
        if (_rootFrame is null)
        {
            return;
        }

        _themes.Apply(UiThemeId.Light);
        _ = _rootFrame.Navigate(typeof(Views.ForcedPinChangePage));
    }

    public void NavigateToMainShell()
    {
        if (_rootFrame is null)
        {
            return;
        }

        if (!_session.IsSignedIn)
        {
            NavigateToLogin();
            return;
        }

        LoginTransitionProbe.Mark("root.nav.mainShell.begin");
        _ = _rootFrame.Navigate(typeof(Views.MainShell));
        LoginTransitionProbe.Mark("root.nav.mainShell.returned");
    }
}
