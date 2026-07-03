using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Mdq = Microsoft.UI.Dispatching;
using NickeltownPOSV4.ViewModels;
using Windows.System;
using Windows.UI.Core;

namespace NickeltownPOSV4.Views.Panels;

public sealed partial class AddDrinksPanel : UserControl
{
    private readonly KeyEventHandler _scanKeyHandler;

    public AddDrinksPanel()
    {
        InitializeComponent();
        _scanKeyHandler = OnRootScanKeyDown;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private AddDrinksPanelViewModel? Vm => DataContext as AddDrinksPanelViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AddDrinksLayoutRoot.AddHandler(UIElement.KeyDownEvent, _scanKeyHandler, handledEventsToo: true);
        _ = Vm?.RefreshCatalogFromDatabaseAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        AddDrinksLayoutRoot.RemoveHandler(UIElement.KeyDownEvent, _scanKeyHandler);
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => EnqueueRefocusScan();

    private void ScanSkuTextBox_Loaded(object sender, RoutedEventArgs e) => EnqueueRefocusScan();

    private void ScanSkuTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        Vm?.CommitPendingScan();
    }

    private void EnqueueRefocusScan()
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
                if (AddDrinksLayoutRoot.Visibility != Visibility.Visible || Vm?.IsBusy == true)
                {
                    return;
                }

                ScanSkuTextBox?.Focus(FocusState.Programmatic);
            });
    }

    /// <summary>
    /// Wedge scanner: buffered keys when focus is not in a text field (SKU box handles typing directly).
    /// Enter commits <see cref="AddDrinksPanelViewModel.CommitPendingScan"/>.
    /// </summary>
    private void OnRootScanKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.OriginalSource is TextBox tb && !ReferenceEquals(tb, ScanSkuTextBox))
        {
            return;
        }

        if (ReferenceEquals(e.OriginalSource, ScanSkuTextBox))
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
            if (!string.IsNullOrWhiteSpace(vm.PendingScanCode))
            {
                vm.CommitPendingScan();
                e.Handled = true;
            }

            return;
        }

        if (e.Key == VirtualKey.Tab && !string.IsNullOrWhiteSpace(vm.PendingScanCode))
        {
            vm.CommitPendingScan();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            if (!string.IsNullOrEmpty(vm.PendingScanCode))
            {
                vm.PendingScanCode = string.Empty;
                e.Handled = true;
            }

            return;
        }

        if (e.Key == VirtualKey.Back)
        {
            vm.BackspacePendingScan();
            e.Handled = true;
            return;
        }

        if (TryMapPrintable(e.Key, out var ch))
        {
            vm.AppendScanChar(ch);
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
