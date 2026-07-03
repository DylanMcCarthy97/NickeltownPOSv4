using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class StockManagementPage
{
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = _vm.LoadAsync();
    }

    private void BackToAdmin_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_vm.IsFullScreenActive)
        {
            StockFullScreenPresenter.Content = null;
            _vm.CurrentScreen = StockManagementScreen.Home;
            return;
        }

        var nav = App.Services.GetRequiredService<INavigationService>();
        if (nav.TryGoBack())
        {
            return;
        }

        nav.Navigate(
            typeof(WorkspacePage),
            new ShellRoute { Id = "admin", Title = "Admin", Glyph = "\uE90F" });
    }
}
