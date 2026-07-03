using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views.Panels;

public sealed partial class NewTabPanel : UserControl
{
    public NewTabPanel(NewTabPanelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
