using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class TabsWorkspaceView : UserControl
{
    public TabsWorkspaceView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<TabsWorkspaceViewModel>();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TabsWorkspaceViewModel vm)
        {
            await vm.RefreshTabsFromDatabaseAsync().ConfigureAwait(true);
        }
    }

    private void BoardCell_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (DataContext is not TabsWorkspaceViewModel vm || sender is not FrameworkElement fe)
        {
            return;
        }

        if (fe.DataContext is not TabsBoardCellViewModel cell)
        {
            return;
        }

        if (cell.IsAddCell)
        {
            vm.OpenAddCardCommand.Execute(null);
            return;
        }

        if (cell.Tab is { } tab)
        {
            vm.SelectTabCommand.Execute(tab);
        }
    }
}
