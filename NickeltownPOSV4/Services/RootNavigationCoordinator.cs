using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.Themes;

namespace NickeltownPOSV4.Services;

public sealed class RootNavigationCoordinator : IRootNavigationCoordinator
{
    private readonly IPosThemeService _themes;

    private Frame? _rootFrame;

    public RootNavigationCoordinator(IPosThemeService themes) => _themes = themes;

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

        _ = _rootFrame.Navigate(typeof(Views.MainShell));
    }
}
