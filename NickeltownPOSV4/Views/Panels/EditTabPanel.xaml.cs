using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views.Panels;

public sealed partial class EditTabPanel : UserControl
{
    public EditTabPanel(EditTabPanelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is EditTabPanelViewModel vm)
        {
            await vm.LoadAsync().ConfigureAwait(true);
        }
    }
}

