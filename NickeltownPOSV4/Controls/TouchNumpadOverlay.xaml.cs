using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Controls;

public sealed partial class TouchNumpadOverlay : UserControl
{
    public TouchNumpadOverlay(TouchNumpadOverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
