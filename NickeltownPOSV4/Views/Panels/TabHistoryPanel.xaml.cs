using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views.Panels;

public sealed partial class TabHistoryPanel : UserControl
{
    public TabHistoryPanel(TabHistoryPanelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TabHistoryPanelViewModel vm)
        {
            await vm.LoadAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Reload when the slide host swaps in this panel again (same instance).</summary>
    public async Task RefreshAsync()
    {
        if (DataContext is TabHistoryPanelViewModel vm)
        {
            await vm.LoadAsync().ConfigureAwait(true);
        }
    }
}
