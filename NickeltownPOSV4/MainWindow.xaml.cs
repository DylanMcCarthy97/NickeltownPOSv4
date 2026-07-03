using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using NickeltownPOSV4.Services;
using Windows.Graphics;
using WinRT.Interop;

namespace NickeltownPOSV4;

public sealed partial class MainWindow : Window
{
    private AppWindow? _appWindow;

    public MainWindow()
    {
        InitializeComponent();

        TcxLayoutDiagnostics.SetUiDispatcher(DispatcherQueue.GetForCurrentThread());

        App.Services.GetRequiredService<IWindowHandleProvider>().Attach(this);

        Title = "Nickeltown POS";

        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.Changed += OnAppWindowChanged;

        ApplyKioskLayout();

        var rootNav = App.Services.GetRequiredService<IRootNavigationCoordinator>();
        rootNav.Attach(RootFrame);
        rootNav.NavigateToStartup();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange || args.DidPresenterChange)
        {
            ApplyKioskLayout();
        }
    }

    private void ApplyKioskLayout()
    {
        if (_appWindow is null)
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
        }

        var appWindow = _appWindow;

        int targetWidth = KioskDisplayOptions.TargetWindowWidth;
        int targetHeight = KioskDisplayOptions.TargetWindowHeight;

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
        }

        appWindow.Resize(new SizeInt32(targetWidth, targetHeight));

        if (KioskDisplayOptions.UseKioskPinnedPosition)
        {
            appWindow.Move(new PointInt32(0, 0));
            return;
        }

        IntPtr hwnd2 = WindowNative.GetWindowHandle(this);
        var windowId2 = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd2);
        var displayArea = DisplayArea.GetFromWindowId(windowId2, DisplayAreaFallback.Nearest);
        var wa = displayArea.WorkArea;
        int x = wa.X + Math.Max(0, (wa.Width - targetWidth) / 2);
        int y = wa.Y + Math.Max(0, (wa.Height - targetHeight) / 2);
        appWindow.Move(new PointInt32(x, y));
    }
}
