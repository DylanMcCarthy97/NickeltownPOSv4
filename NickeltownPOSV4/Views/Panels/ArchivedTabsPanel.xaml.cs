using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views.Panels;

public sealed partial class ArchivedTabsPanel : UserControl
{
    public ArchivedTabsPanel(ArchivedTabsPanelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
