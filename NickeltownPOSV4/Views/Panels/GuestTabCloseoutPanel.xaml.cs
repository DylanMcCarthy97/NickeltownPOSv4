using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views.Panels;

public sealed partial class GuestTabCloseoutPanel : UserControl
{
    public GuestTabCloseoutPanel(GuestTabCloseoutPanelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is GuestTabCloseoutPanelViewModel vm)
        {
            vm.RefreshFromSession();
        }

        Loaded -= OnLoaded;
    }
}
