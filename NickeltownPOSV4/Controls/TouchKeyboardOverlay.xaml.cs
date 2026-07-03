using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Controls;

public sealed partial class TouchKeyboardOverlay : UserControl
{
    public TouchKeyboardOverlay(TouchKeyboardOverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Button marks pointer routes as handled; register with handledEventsToo so Shift press/hold still works.
        ShiftKeyButton.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => viewModel.OnShiftPointerPressed()), handledEventsToo: true);
        ShiftKeyButton.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => viewModel.OnShiftPointerReleased()), handledEventsToo: true);
        ShiftKeyButton.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler((_, _) => viewModel.OnShiftPointerReleased()), handledEventsToo: true);
        ShiftKeyButton.PointerCaptureLost += (_, _) => viewModel.OnShiftPointerReleased();

        ShiftKeyButton.Click += (_, _) => viewModel.OnShiftClick();
    }
}
