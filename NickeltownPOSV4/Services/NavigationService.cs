using System;
using Microsoft.UI.Xaml.Controls;

namespace NickeltownPOSV4.Services;

public sealed class NavigationService : INavigationService
{
    private Frame? _shellFrame;

    public Type? CurrentPageType => _shellFrame?.CurrentSourcePageType;

    public void AttachShellFrame(Frame shellContentFrame)
    {
        _shellFrame = shellContentFrame;
    }

    public bool Navigate(Type pageType, object? parameter = null)
    {
        if (_shellFrame is null)
        {
            return false;
        }

        return _shellFrame.Navigate(pageType, parameter);
    }

    public bool Navigate<TPage>() where TPage : class
    {
        return Navigate(typeof(TPage));
    }

    public bool TryGoBack()
    {
        if (_shellFrame is null || !_shellFrame.CanGoBack)
        {
            return false;
        }

        _shellFrame.GoBack();
        return true;
    }
}
