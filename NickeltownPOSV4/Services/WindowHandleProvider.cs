using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace NickeltownPOSV4.Services;

public sealed class WindowHandleProvider : IWindowHandleProvider
{
    private Window? _window;

    public nint WindowHandle => _window is null ? 0 : WindowNative.GetWindowHandle(_window);

    public XamlRoot? GetXamlRoot() => _window?.Content?.XamlRoot;

    public void Attach(Window window) => _window = window;
}

