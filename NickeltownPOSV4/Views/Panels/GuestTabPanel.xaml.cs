using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views.Panels;

public sealed partial class GuestTabPanel : UserControl
{
    public GuestTabPanel(GuestTabPanelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
