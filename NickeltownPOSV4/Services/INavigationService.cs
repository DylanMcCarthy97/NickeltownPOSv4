using System;
using Microsoft.UI.Xaml.Controls;

namespace NickeltownPOSV4.Services;

public interface INavigationService
{
    void AttachShellFrame(Frame shellContentFrame);

    bool Navigate(Type pageType, object? parameter = null);

    bool Navigate<TPage>() where TPage : class;

    Type? CurrentPageType { get; }

    /// <summary>Returns true if the shell frame went back one entry.</summary>
    bool TryGoBack();
}
