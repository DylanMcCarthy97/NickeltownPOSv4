using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views.Panels;

public sealed partial class GuestCloseoutPanel : UserControl
{
    public GuestCloseoutPanel(GuestCloseoutPanelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
