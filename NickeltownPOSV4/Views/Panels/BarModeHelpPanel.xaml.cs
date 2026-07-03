using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views.Panels;

public sealed partial class BarModeHelpPanel : UserControl
{
    public BarModeHelpPanel(BarModeHelpPanelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
