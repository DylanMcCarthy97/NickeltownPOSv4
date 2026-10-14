using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Logging;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class MainShell : Page
{
    public MainShell()
    {
        LoginTransitionProbe.Mark("mainShell.ctor.begin");
        InitializeComponent();
        LoginTransitionProbe.Mark("mainShell.ctor.xaml");
        DataContext = App.Services.GetRequiredService<MainShellViewModel>();
        Loaded += OnLoaded;
        LoginTransitionProbe.Mark("mainShell.ctor.done");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        LoginTransitionProbe.Mark("mainShell.loaded");

        App.Services.GetRequiredService<ISlidePanelService>().Attach(SlidePanelHost);
        App.Services.GetRequiredService<IInputOverlayService>().Attach(InputOverlayHost);

        if (DataContext is MainShellViewModel vm)
        {
            ShellContentFrame.CacheSize = 8;
            ShellContentFrame.Navigated += OnShellContentNavigated;
            vm.InitializeShell(ShellContentFrame);
            LoginTransitionProbe.Mark("mainShell.initializeShell.done");
        }
    }

    private void OnShellContentNavigated(object sender, NavigationEventArgs e) =>
        App.Services.GetRequiredService<ISessionInactivityService>().NotifyActivity();

    private void RouteButton_Click(object sender, RoutedEventArgs e)
    {
        App.Services.GetRequiredService<ISessionInactivityService>().NotifyActivity();
        if (sender is not Button button || button.Tag is not string tag || !int.TryParse(tag, out var index))
        {
            return;
        }

        if (DataContext is not MainShellViewModel vm || index < 0 || index >= vm.Routes.Count)
        {
            return;
        }

        vm.NavigateToCommand.Execute(vm.Routes[index]);
    }
}
