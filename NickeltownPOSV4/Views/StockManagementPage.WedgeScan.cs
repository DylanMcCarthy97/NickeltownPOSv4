using System;
using System.Threading.Tasks;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace NickeltownPOSV4.Views;

/// <summary>
/// Stock v2 wedge-scanner support: buffer keys anywhere on the home screen and
/// open the matching item on Enter — no need to tap Search first.
/// </summary>
public sealed partial class StockManagementPage
{
    private KeyEventHandler? _stockScanKeyHandler;
    private string _wedgeScanBuffer = string.Empty;
    private DateTime _wedgeScanLastKeyUtc = DateTime.MinValue;

    private void AttachStockWedgeScan()
    {
        if (_stockScanKeyHandler is not null || StockPageRoot is null)
        {
            return;
        }

        _stockScanKeyHandler = OnStockRootScanKeyDown;
        StockPageRoot.AddHandler(UIElement.KeyDownEvent, _stockScanKeyHandler, handledEventsToo: true);
        StockPageRoot.IsTabStop = true;
        try
        {
            StockPageRoot.Focus(FocusState.Programmatic);
        }
        catch
        {
            // Focus can fail before the visual tree is ready.
        }
    }

    private void DetachStockWedgeScan()
    {
        if (_stockScanKeyHandler is null || StockPageRoot is null)
        {
            return;
        }

        StockPageRoot.RemoveHandler(UIElement.KeyDownEvent, _stockScanKeyHandler);
        _stockScanKeyHandler = null;
        _wedgeScanBuffer = string.Empty;
    }

    private void ClearStockSearchFocus()
    {
        try
        {
            StockPageRoot?.Focus(FocusState.Programmatic);
        }
        catch
        {
            // Ignore focus races while overlays close.
        }
    }

    private async void OnStockRootScanKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Never steal keys while an overlay TextBox/numpad field has focus, or while a modal is open.
        if (StockModalLayer is not null && StockModalLayer.Visibility == Visibility.Visible)
        {
            return;
        }

        if (!IsHomeScreenForScan())
        {
            return;
        }

        if (e.OriginalSource is TextBox)
        {
            return;
        }

        if (_vm.IsBusy || IsControlDown())
        {
            return;
        }

        // Reset buffer if the operator paused between digits (not a continuous wedge burst).
        if ((DateTime.UtcNow - _wedgeScanLastKeyUtc).TotalMilliseconds > 450)
        {
            _wedgeScanBuffer = string.Empty;
        }

        _wedgeScanLastKeyUtc = DateTime.UtcNow;

        if (e.Key is VirtualKey.Enter or VirtualKey.Tab)
        {
            if (string.IsNullOrWhiteSpace(_wedgeScanBuffer))
            {
                return;
            }

            var code = _wedgeScanBuffer.Trim();
            _wedgeScanBuffer = string.Empty;
            e.Handled = true;
            await TryHandleHomeBarcodeOrSearchAsync(code).ConfigureAwait(true);
            ClearStockSearchFocus();
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            if (_wedgeScanBuffer.Length > 0)
            {
                _wedgeScanBuffer = string.Empty;
                e.Handled = true;
            }

            return;
        }

        if (e.Key == VirtualKey.Back)
        {
            if (_wedgeScanBuffer.Length > 0)
            {
                _wedgeScanBuffer = _wedgeScanBuffer[..^1];
                e.Handled = true;
            }

            return;
        }

        if (TryMapPrintableScanKey(e.Key, out var ch))
        {
            if (_wedgeScanBuffer.Length < 64)
            {
                _wedgeScanBuffer += ch;
            }

            e.Handled = true;
        }
    }

    private bool IsHomeScreenForScan() =>
        _vm.IsHomeScreen
        && (StockFullScreenHost is null || StockFullScreenHost.Visibility != Visibility.Visible);

    private static bool IsControlDown()
    {
        var left = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftControl);
        var right = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightControl);
        return (left & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down
            || (right & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private static bool TryMapPrintableScanKey(VirtualKey key, out char ch)
    {
        var shift = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down)
            == CoreVirtualKeyStates.Down;

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

        if (key == VirtualKey.Subtract || key == (VirtualKey)189)
        {
            ch = shift ? '_' : '-';
            return true;
        }

        if (key == VirtualKey.Space)
        {
            ch = ' ';
            return true;
        }

        if (key == VirtualKey.Decimal || key == (VirtualKey)190)
        {
            ch = '.';
            return true;
        }

        if (key == VirtualKey.Divide || key == (VirtualKey)191)
        {
            ch = '/';
            return true;
        }

        ch = default;
        return false;
    }
}
