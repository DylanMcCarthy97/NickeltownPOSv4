using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views.Panels;

public sealed partial class PitstopHeldSalesPanel : UserControl
{
    public PitstopHeldSalesPanel(PitstopHeldSalesPanelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
