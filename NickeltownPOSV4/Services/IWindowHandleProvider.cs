using Microsoft.UI.Xaml;

namespace NickeltownPOSV4.Services;

/// <summary>Provides the HWND used to parent WinRT pickers and dialogs.</summary>
public interface IWindowHandleProvider
{
    nint WindowHandle { get; }

    Microsoft.UI.Xaml.XamlRoot? GetXamlRoot();

    void Attach(Window window);
}
