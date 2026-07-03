using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Controls;

public sealed partial class TouchDatePickerOverlay : UserControl
{
    public TouchDatePickerOverlay(TouchDatePickerOverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
