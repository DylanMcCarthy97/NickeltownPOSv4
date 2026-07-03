using System;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;

namespace NickeltownPOSV4.ViewModels;

public sealed class TouchKeyboardOverlayViewModel : ObservableViewModel
{
    private const int ShiftHoldMs = 420;

    private readonly Action<string?> _finish;
    private readonly DispatcherQueueTimer _shiftHoldTimer;
    private string _text;
    private bool _capsLock;
    private bool _shiftArmed;
    private bool _isNumbersMode;
    private bool _shiftPointerDown;
    private bool _longHoldFired;

    public TouchKeyboardOverlayViewModel(string initialValue, string title, Action<string?> finish)
    {
        _finish = finish;
        _text = initialValue ?? string.Empty;
        Title = string.IsNullOrWhiteSpace(title) ? "Type Note" : title.Trim();

        _shiftHoldTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _shiftHoldTimer.Interval = TimeSpan.FromMilliseconds(ShiftHoldMs);
        _shiftHoldTimer.IsRepeating = false;
        _shiftHoldTimer.Tick += OnShiftHoldTimerTick;

        KeyCommand = new RelayCommand<string>(AppendLetterKey);
        DigitCommand = new RelayCommand<string>(AppendDigitOrSymbol);
        BackspaceCommand = new RelayCommand(Backspace);
        SpaceCommand = new RelayCommand(AppendSpace);
        DotCommand = new RelayCommand(() => AppendRaw("."));
        CommaCommand = new RelayCommand(() => AppendRaw(","));
        ToggleNumbersLayoutCommand = new RelayCommand(ToggleNumbersLayout);
        DoneCommand = new RelayCommand(() => _finish(Text));
        CancelCommand = new RelayCommand(() => _finish(null));
        ClearCommand = new RelayCommand(ClearAll);
    }

    public string Title { get; }

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value ?? string.Empty);
    }

    /// <summary>True when letter keys should show uppercase labels (caps lock or one-shot shift armed).</summary>
    public bool UppercaseLabels => CapsLock || ShiftArmed;

    /// <summary>Highlight the Shift key for caps lock or armed one-shot shift.</summary>
    public bool ShiftKeyHighlighted => CapsLock || ShiftArmed;

    public bool CapsLock
    {
        get => _capsLock;
        private set
        {
            if (!SetProperty(ref _capsLock, value))
            {
                return;
            }

            RaiseShiftUiChanged();
        }
    }

    public bool ShiftArmed
    {
        get => _shiftArmed;
        private set
        {
            if (!SetProperty(ref _shiftArmed, value))
            {
                return;
            }

            RaiseShiftUiChanged();
        }
    }

    public bool IsNumbersMode
    {
        get => _isNumbersMode;
        private set
        {
            if (!SetProperty(ref _isNumbersMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(NumbersToggleButtonLabel));
        }
    }

    public string NumbersToggleButtonLabel => IsNumbersMode ? "ABC" : "123";

    public IRelayCommand<string> KeyCommand { get; }

    public IRelayCommand<string> DigitCommand { get; }

    public IRelayCommand BackspaceCommand { get; }

    public IRelayCommand SpaceCommand { get; }

    public IRelayCommand DotCommand { get; }

    public IRelayCommand CommaCommand { get; }

    public IRelayCommand ToggleNumbersLayoutCommand { get; }

    public IRelayCommand DoneCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IRelayCommand ClearCommand { get; }

    /// <summary>Pointer down on Shift (use routed handler with <c>handledEventsToo</c> on the button).</summary>
    public void OnShiftPointerPressed()
    {
        _shiftPointerDown = true;
        _longHoldFired = false;
        _shiftHoldTimer.Stop();
        _shiftHoldTimer.Start();
    }

    /// <summary>Pointer up — stops hold timer; short tap is handled by <see cref="OnShiftClick"/>.</summary>
    public void OnShiftPointerReleased()
    {
        _shiftPointerDown = false;
        _shiftHoldTimer.Stop();
    }

    private void OnShiftHoldTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        if (!_shiftPointerDown)
        {
            return;
        }

        CapsLock = true;
        ShiftArmed = false;
        _longHoldFired = true;
    }

    /// <summary>Button Click — one-shot shift / exit caps. Suppressed once after hold-to-lock.</summary>
    public void OnShiftClick()
    {
        if (_longHoldFired)
        {
            _longHoldFired = false;
            return;
        }

        OnShiftShortTap();
    }

    /// <summary>Short tap: exit caps lock, or arm/disarm one capital letter.</summary>
    private void OnShiftShortTap()
    {
        if (CapsLock)
        {
            CapsLock = false;
            ShiftArmed = false;
            return;
        }

        ShiftArmed = !ShiftArmed;
    }

    private void RaiseShiftUiChanged()
    {
        OnPropertyChanged(nameof(UppercaseLabels));
        OnPropertyChanged(nameof(ShiftKeyHighlighted));
    }

    private void AppendLetterKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length != 1)
        {
            return;
        }

        var ch = key[0];
        if (!char.IsLetter(ch))
        {
            AppendRaw(key);
            return;
        }

        var upper = CapsLock || ShiftArmed;
        AppendRaw(upper ? char.ToUpperInvariant(ch).ToString() : char.ToLowerInvariant(ch).ToString());
        if (!CapsLock && ShiftArmed)
        {
            ShiftArmed = false;
        }
    }

    private void AppendDigitOrSymbol(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        AppendRaw(key);
    }

    private void ToggleNumbersLayout()
    {
        IsNumbersMode = !IsNumbersMode;
    }

    private void AppendSpace()
    {
        AppendRaw(" ");
        ShiftArmed = false;
    }

    private void ClearAll()
    {
        Text = string.Empty;
        ShiftArmed = false;
        CapsLock = false;
    }

    private void AppendRaw(string raw)
    {
        if (Text.Length >= 120)
        {
            return;
        }

        Text += raw;
    }

    private void Backspace()
    {
        if (Text.Length == 0)
        {
            return;
        }

        Text = Text[..^1];
    }
}
