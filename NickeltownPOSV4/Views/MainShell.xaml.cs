using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class MainShell : Page
{
    public MainShell()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MainShellViewModel>();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        App.Services.GetRequiredService<ISlidePanelService>().Attach(SlidePanelHost);
        App.Services.GetRequiredService<IInputOverlayService>().Attach(InputOverlayHost);

        if (DataContext is MainShellViewModel vm)
        {
            ShellContentFrame.CacheSize = 8;
            vm.InitializeShell(ShellContentFrame);
        }
    }

    private void RouteButton_Click(object sender, RoutedEventArgs e)
    {
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
