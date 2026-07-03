using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views.Panels;

public sealed partial class StockEditorPanel : UserControl
{
    public StockEditorPanel(StockEditorPanelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
