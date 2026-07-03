using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services;

public sealed class InputOverlayService : IInputOverlayService
{
    private InputOverlayHost? _host;
    private TaskCompletionSource<object?>? _pending;

    public bool IsOpen => _host?.IsOpen == true;

    public void Attach(InputOverlayHost host)
    {
        _host = host;
        _host.BackgroundDismissed -= OnBackgroundDismissed;
        _host.BackgroundDismissed += OnBackgroundDismissed;
    }

    public void Close()
    {
        _pending?.TrySetResult(null);
        _pending = null;
        _host?.Close();
    }

    public async Task<decimal?> ShowNumpadAsync(
        decimal initialValue,
        string title,
        bool allowSignedAmount = false,
        CancellationToken cancellationToken = default)
    {
        EnsureHostAttached();
        Close();

        var tcs = NewPending(cancellationToken);
        var vm = new TouchNumpadOverlayViewModel(initialValue, title, allowSignedAmount, result =>
        {
            tcs.TrySetResult(result);
            _host?.Close();
        });
        var control = new TouchNumpadOverlay(vm);
        _host!.Open(control);

        var raw = await tcs.Task.ConfigureAwait(true);
        _pending = null;
        return raw is decimal d ? d : null;
    }

    public async Task<string?> ShowKeyboardAsync(string initialValue, string title, CancellationToken cancellationToken = default)
    {
        EnsureHostAttached();
        Close();

        var tcs = NewPending(cancellationToken);
        var vm = new TouchKeyboardOverlayViewModel(initialValue, title, result =>
        {
            tcs.TrySetResult(result);
            _host?.Close();
        });
        var control = new TouchKeyboardOverlay(vm);
        _host!.Open(control);

        var raw = await tcs.Task.ConfigureAwait(true);
        _pending = null;
        return raw as string;
    }

    public async Task<int?> ShowIntegerNumpadAsync(
        int initialValue,
        string title,
        int min = 0,
        int max = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        EnsureHostAttached();
        Close();

        var tcs = NewPending(cancellationToken);
        var vm = new TouchNumpadOverlayViewModel(
            initialValue: initialValue,
            title: title,
            allowSignedAmount: false,
            mode: NumpadMode.Integer,
            maxLength: max.ToString(System.Globalization.CultureInfo.InvariantCulture).Length,
            maskDisplay: false,
            allowEmpty: false,
            finish: _ => { },
            finishRaw: result =>
            {
                tcs.TrySetResult(result);
                _host?.Close();
            });
        var control = new TouchNumpadOverlay(vm);
        _host!.Open(control);

        var raw = await tcs.Task.ConfigureAwait(true);
        _pending = null;
        if (raw is string s && int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var iv))
        {
            if (iv < min)
            {
                iv = min;
            }
            else if (iv > max)
            {
                iv = max;
            }

            return iv;
        }

        return null;
    }

    public async Task<string?> ShowPinNumpadAsync(
        string title,
        int digitCount = 4,
        bool maskDisplay = true,
        CancellationToken cancellationToken = default)
    {
        EnsureHostAttached();
        Close();

        var tcs = NewPending(cancellationToken);
        var vm = new TouchNumpadOverlayViewModel(
            initialValue: 0m,
            title: title,
            allowSignedAmount: false,
            mode: NumpadMode.Pin,
            maxLength: digitCount,
            maskDisplay: maskDisplay,
            allowEmpty: false,
            finish: _ => { },
            finishRaw: result =>
            {
                tcs.TrySetResult(result);
                _host?.Close();
            });
        var control = new TouchNumpadOverlay(vm);
        _host!.Open(control);

        var raw = await tcs.Task.ConfigureAwait(true);
        _pending = null;
        return raw as string;
    }

    public async Task<string?> ShowDigitStringNumpadAsync(
        string initialValue,
        string title,
        int maxLength = 15,
        bool allowEmpty = true,
        CancellationToken cancellationToken = default)
    {
        EnsureHostAttached();
        Close();

        var tcs = NewPending(cancellationToken);
        var vm = new TouchNumpadOverlayViewModel(
            initialDigits: initialValue,
            title: title,
            maxLength: maxLength,
            allowEmpty: allowEmpty,
            finishRaw: result =>
            {
                tcs.TrySetResult(result);
                _host?.Close();
            });
        var control = new TouchNumpadOverlay(vm);
        _host!.Open(control);

        var raw = await tcs.Task.ConfigureAwait(true);
        _pending = null;
        return raw as string;
    }

    public async Task<DatePickerOverlayResult> ShowDatePickerAsync(
        DateOnly? initialValue,
        string title,
        int? minYear = null,
        int? maxYear = null,
        CancellationToken cancellationToken = default)
    {
        EnsureHostAttached();
        Close();

        var min = minYear ?? 1920;
        var max = maxYear ?? DateTime.Today.Year + 10;

        var tcs = NewPending(cancellationToken);
        var vm = new TouchDatePickerOverlayViewModel(
            initialValue,
            title,
            result =>
            {
                tcs.TrySetResult(result);
                _host?.Close();
            },
            min,
            max);
        var control = new TouchDatePickerOverlay(vm);
        _host!.Open(control);

        var raw = await tcs.Task.ConfigureAwait(true);
        _pending = null;
        return raw is DatePickerOverlayResult r ? r : new DatePickerOverlayResult(Cancelled: true, Value: null);
    }

    private TaskCompletionSource<object?> NewPending(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = tcs;

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                tcs.TrySetResult(null);
                _host?.Close();
            });
        }

        return tcs;
    }

    private void OnBackgroundDismissed(object? sender, EventArgs e) => Close();

    private void EnsureHostAttached()
    {
        if (_host is null)
        {
            throw new InvalidOperationException("InputOverlayHost is not attached.");
        }
    }
}
