using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Mdq = Microsoft.UI.Dispatching;
using NickeltownPOSV4.ViewModels;
using Windows.System;
using Windows.UI.Core;

namespace NickeltownPOSV4.Views;

public sealed partial class PitstopRetailView : UserControl
{
    private readonly KeyEventHandler _scanKeyHandler;

    public PitstopRetailView()
    {
        InitializeComponent();
        _scanKeyHandler = OnRootScanKeyDown;
        DataContext = App.Services.GetRequiredService<PitstopRetailViewModel>();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private PitstopRetailViewModel? Vm => DataContext as PitstopRetailViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PitstopLayoutRoot.AddHandler(UIElement.KeyDownEvent, _scanKeyHandler, handledEventsToo: true);
        if (DataContext is PitstopRetailViewModel vm)
        {
            _ = vm.InitializeAsync();
            _ = vm.RefreshCatalogFromDatabaseAsync();
        }

        EnqueueRefocusBarcode();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        PitstopLayoutRoot.RemoveHandler(UIElement.KeyDownEvent, _scanKeyHandler);
    }

    private void EnqueueRefocusBarcode()
    {
        var dq = Mdq.DispatcherQueue.GetForCurrentThread();
        if (dq is null)
        {
            return;
        }

        _ = dq.TryEnqueue(
            Mdq.DispatcherQueuePriority.Low,
            () =>
            {
                if (Vm?.IsBusy == true)
                {
                    return;
                }

                try
                {
                    BarcodeEntry?.Focus(FocusState.Programmatic);
                }
                catch
                {
                }
            });
    }

    private void CartQtyButton_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        e.Handled = true;

    private void CartLine_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not PitstopCartLineViewModel line)
        {
            return;
        }

        if (e.OriginalSource is Button)
        {
            return;
        }

        if (Vm is not null)
        {
            Vm.SelectedCartLine = line;
        }

        e.Handled = true;
    }

    private async void BarcodeEntry_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        if (DataContext is PitstopRetailViewModel vm)
        {
            await vm.TryCompleteBarcodeAsync().ConfigureAwait(true);
            EnqueueRefocusBarcode();
        }
    }

    private void OnRootScanKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.OriginalSource is TextBox tb && !ReferenceEquals(tb, BarcodeEntry))
        {
            return;
        }

        if (ReferenceEquals(e.OriginalSource, BarcodeEntry))
        {
            return;
        }

        var vm = Vm;
        if (vm is null || vm.IsBusy || IsControlDown())
        {
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            if (!string.IsNullOrWhiteSpace(vm.BarcodeBuffer))
            {
                vm.CommitBarcodeScan();
                e.Handled = true;
                EnqueueRefocusBarcode();
            }

            return;
        }

        if (e.Key == VirtualKey.Tab && !string.IsNullOrWhiteSpace(vm.BarcodeBuffer))
        {
            vm.CommitBarcodeScan();
            e.Handled = true;
            EnqueueRefocusBarcode();
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            if (!string.IsNullOrEmpty(vm.BarcodeBuffer))
            {
                vm.BarcodeBuffer = string.Empty;
                e.Handled = true;
            }

            return;
        }

        if (e.Key == VirtualKey.Back)
        {
            vm.BackspaceBarcode();
            e.Handled = true;
            return;
        }

        if (TryMapPrintable(e.Key, out var ch))
        {
            vm.AppendBarcodeChar(ch);
            e.Handled = true;
        }
    }

    private static bool IsControlDown()
    {
        var left = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftControl);
        var right = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightControl);
        return (left & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down
            || (right & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private static bool TryMapPrintable(VirtualKey key, out char ch)
    {
        var shift = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

        if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
        {
            ch = (char)('0' + (key - VirtualKey.Number0));
            return true;
        }

        if (key >= VirtualKey.NumberPad0 && key <= VirtualKey.NumberPad9)
        {
            ch = (char)('0' + (key - VirtualKey.NumberPad0));
            return true;
        }

        if (key >= VirtualKey.A && key <= VirtualKey.Z)
        {
            var c = (char)('A' + (key - VirtualKey.A));
            ch = shift ? c : char.ToLowerInvariant(c);
            return true;
        }

        if (key == VirtualKey.Subtract)
        {
            ch = shift ? '_' : '-';
            return true;
        }

        if (key == VirtualKey.Space)
        {
            ch = ' ';
            return true;
        }

        if (key == VirtualKey.Multiply)
        {
            ch = '*';
            return true;
        }

        if (key == VirtualKey.Add)
        {
            ch = '+';
            return true;
        }

        if (key == VirtualKey.Divide)
        {
            ch = '/';
            return true;
        }

        if (key == VirtualKey.Decimal)
        {
            ch = '.';
            return true;
        }

        if (key == (VirtualKey)190)
        {
            ch = shift ? '>' : '.';
            return true;
        }

        if (key == (VirtualKey)191)
        {
            ch = shift ? '?' : '/';
            return true;
        }

        ch = default;
        return false;
    }
}
