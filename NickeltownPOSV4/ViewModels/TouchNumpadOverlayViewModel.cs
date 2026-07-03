using System;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NickeltownPOSV4.ViewModels;

/// <summary>Keypad behaviour for <see cref="TouchNumpadOverlayViewModel"/>.</summary>
public enum NumpadMode
{
    /// <summary>Two-decimal numeric entry (e.g. money), displays plain "X.XX" (no currency symbol). Returns a decimal.</summary>
    Currency,

    /// <summary>Whole-number entry (e.g. port numbers). Hides "." and "00". Returns a decimal whose integral part is the entered value.</summary>
    Integer,

    /// <summary>Fixed-length digit string (PIN). Hides "." and "00". DoneCommand fires automatically at <c>MaxLength</c>. Returns the digit string via the raw-text path.</summary>
    Pin,

    /// <summary>Variable-length digit string (e.g. phone). Hides "." and "00". Returns the digit string via the raw-text path.</summary>
    Digits,
}

public sealed class TouchNumpadOverlayViewModel : ObservableViewModel
{
    private readonly Action<decimal?> _finish;

    private readonly Action<string?>? _finishRaw;

    private readonly bool _allowSigned;

    private readonly NumpadMode _mode;

    private readonly int _maxLength;

    private readonly bool _maskDisplay;

    private readonly bool _allowEmpty;

    private string _draft;

    public TouchNumpadOverlayViewModel(
        decimal initialValue,
        string title,
        bool allowSignedAmount,
        Action<decimal?> finish)
        : this(initialValue, title, allowSignedAmount, NumpadMode.Currency, 0, false, true, finish, null)
    {
    }

    /// <summary>Variable-length digit string entry (e.g. phone numbers).</summary>
    public TouchNumpadOverlayViewModel(
        string initialDigits,
        string title,
        int maxLength,
        bool allowEmpty,
        Action<string?> finishRaw)
        : this(0m, title, false, NumpadMode.Digits, maxLength, false, allowEmpty, _ => { }, finishRaw)
    {
        _draft = SanitizeDigits(initialDigits);
    }

    /// <summary>Full constructor used by the input overlay service for integer / PIN modes.</summary>
    public TouchNumpadOverlayViewModel(
        decimal initialValue,
        string title,
        bool allowSignedAmount,
        NumpadMode mode,
        int maxLength,
        bool maskDisplay,
        bool allowEmpty,
        Action<decimal?> finish,
        Action<string?>? finishRaw)
    {
        _finish = finish;
        _finishRaw = finishRaw;
        _allowSigned = allowSignedAmount && mode == NumpadMode.Currency;
        _mode = mode;
        _maskDisplay = maskDisplay;
        _allowEmpty = allowEmpty;
        _maxLength = maxLength <= 0
            ? mode switch
            {
                NumpadMode.Pin => 4,
                NumpadMode.Integer => 12,
                NumpadMode.Digits => 15,
                _ => 10,
            }
            : maxLength;

        Title = string.IsNullOrWhiteSpace(title)
            ? mode switch
            {
                NumpadMode.Pin => "Enter PIN",
                NumpadMode.Integer => "Enter Number",
                NumpadMode.Digits => "Enter Number",
                _ => "Enter Amount",
            }
            : title.Trim();

        _draft = mode == NumpadMode.Digits ? string.Empty : BuildInitialDraft(initialValue);

        DigitCommand = new RelayCommand<string>(AppendDigit);
        BackspaceCommand = new RelayCommand(Backspace);
        ClearCommand = new RelayCommand(Clear);
        CancelCommand = new RelayCommand(() => Finish(null, null));
        DoneCommand = new RelayCommand(Done, CanDone);
        ToggleSignCommand = new RelayCommand(ToggleSign, () => _allowSigned);
    }

    public string Title { get; }

    public bool AllowsSignedAmount => _allowSigned;

    public bool IsCurrencyMode => _mode == NumpadMode.Currency;

    public bool IsIntegerMode => _mode == NumpadMode.Integer;

    public bool IsPinMode => _mode == NumpadMode.Pin;

    public bool IsDigitsMode => _mode == NumpadMode.Digits;

    public bool ShowDecimalKeys => _mode == NumpadMode.Currency;

    public string AmountDisplay =>
        _mode switch
        {
            NumpadMode.Currency => FormatCurrencyDisplay(),
            NumpadMode.Integer => FormatIntegerDisplay(),
            NumpadMode.Digits => FormatDigitsDisplay(),
            NumpadMode.Pin => FormatPinDisplay(),
            _ => string.IsNullOrEmpty(_draft) ? "0" : _draft,
        };

    public IRelayCommand<string> DigitCommand { get; }

    public IRelayCommand BackspaceCommand { get; }

    public IRelayCommand ClearCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IRelayCommand DoneCommand { get; }

    public IRelayCommand ToggleSignCommand { get; }

    /// <summary>Reads the current currency draft as a decimal (false when empty/invalid).</summary>
    public bool TryPeekCurrency(out decimal value) => TryParseCurrency(_draft, out value);

    /// <summary>Replaces the currency draft (currency mode only).</summary>
    public void ResetCurrencyDraft(decimal initialValue)
    {
        if (_mode != NumpadMode.Currency)
        {
            return;
        }

        _draft = BuildInitialDraft(initialValue);
        RaiseDraftChanged();
    }

    private string BuildInitialDraft(decimal initialValue)
    {
        if (_mode == NumpadMode.Pin)
        {
            return string.Empty;
        }

        if (initialValue == 0m)
        {
            return string.Empty;
        }

        if (_mode == NumpadMode.Integer)
        {
            var truncated = (long)Math.Truncate(initialValue);
            return truncated.ToString(CultureInfo.InvariantCulture);
        }

        var r = decimal.Round(initialValue, 2, MidpointRounding.AwayFromZero);
        return r.ToString("0.00", CultureInfo.InvariantCulture).TrimStart('+');
    }

    private string FormatCurrencyDisplay()
    {
        if (string.IsNullOrWhiteSpace(_draft))
        {
            return "0.00";
        }

        return FormatDecimalPlain(ParseCurrencyOrZero());
    }

    private string FormatIntegerDisplay()
    {
        if (string.IsNullOrWhiteSpace(_draft))
        {
            return "0";
        }

        // Strip leading zeros for display but keep at least one digit.
        var trimmed = _draft.TrimStart('0');
        return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
    }

    private string FormatDigitsDisplay() =>
        string.IsNullOrEmpty(_draft) ? "—" : _draft;

    private string FormatPinDisplay()
    {
        var len = _draft.Length;
        if (len == 0)
        {
            return new string('•', Math.Max(_maxLength, 1)).Replace('•', '_');
        }

        if (_maskDisplay)
        {
            var dots = new string('•', len);
            var rest = new string('_', Math.Max(0, _maxLength - len));
            return dots + rest;
        }

        var pad = new string('_', Math.Max(0, _maxLength - len));
        return _draft + pad;
    }

    private void AppendDigit(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        if (_mode == NumpadMode.Pin)
        {
            if (token.Length != 1 || !char.IsDigit(token[0]))
            {
                return;
            }

            if (_draft.Length >= _maxLength)
            {
                return;
            }

            _draft += token;
            RaiseDraftChanged();

            if (_mode == NumpadMode.Pin && _draft.Length >= _maxLength)
            {
                // Auto-submit once full PIN is entered.
                if (CanDone())
                {
                    Done();
                }
            }

            return;
        }

        if (_mode == NumpadMode.Integer || _mode == NumpadMode.Digits)
        {
            if (token.Length != 1 || !char.IsDigit(token[0]))
            {
                return;
            }

            if (_draft.Length >= _maxLength)
            {
                return;
            }

            _draft += token;
            RaiseDraftChanged();
            return;
        }

        var next = token switch
        {
            "00" => _draft + "00",
            "." => _draft.Contains('.') ? _draft : (_draft.Length == 0 ? "0." : _draft + "."),
            _ => _draft + token,
        };

        if (!IsValidCurrencyDraft(next))
        {
            return;
        }

        _draft = next;
        RaiseDraftChanged();
    }

    private void Backspace()
    {
        if (_draft.Length == 0)
        {
            return;
        }

        _draft = _draft[..^1];
        RaiseDraftChanged();
    }

    private void Clear()
    {
        _draft = string.Empty;
        RaiseDraftChanged();
    }

    private void ToggleSign()
    {
        if (!_allowSigned)
        {
            return;
        }

        if (string.IsNullOrEmpty(_draft) || _draft == "-")
        {
            _draft = _draft.StartsWith("-", StringComparison.Ordinal) ? string.Empty : "-";
            RaiseDraftChanged();
            return;
        }

        if (_draft.StartsWith("-", StringComparison.Ordinal))
        {
            _draft = _draft[1..];
        }
        else
        {
            _draft = "-" + _draft;
        }

        if (!IsValidCurrencyDraft(_draft))
        {
            _draft = _draft.TrimStart('-');
        }

        RaiseDraftChanged();
    }

    private void Done()
    {
        if (_mode == NumpadMode.Pin)
        {
            if (_draft.Length != _maxLength)
            {
                return;
            }

            Finish(null, _draft);
            return;
        }

        if (_mode == NumpadMode.Digits)
        {
            Finish(null, _draft);
            return;
        }

        if (_mode == NumpadMode.Integer)
        {
            if (!long.TryParse(_draft, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
            {
                return;
            }

            Finish((decimal)iv, _draft);
            return;
        }

        if (!TryParseCurrency(_draft, out var value))
        {
            return;
        }

        Finish(value, null);
    }

    private void Finish(decimal? value, string? raw)
    {
        if (_finishRaw is not null)
        {
            _finishRaw(raw);
            return;
        }

        _finish(value);
    }

    private bool CanDone()
    {
        return _mode switch
        {
            NumpadMode.Pin => _draft.Length == _maxLength,
            NumpadMode.Digits => _allowEmpty || _draft.Length > 0,
            NumpadMode.Integer => long.TryParse(_draft, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            _ => TryParseCurrency(_draft, out _),
        };
    }

    private static string SanitizeDigits(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsDigit).ToArray());
    }

    private bool TryParseCurrency(string? text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text) || text == "-")
        {
            return false;
        }

        var t = text.Trim();
        if (!decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        value = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        if (!_allowSigned && value < 0m)
        {
            return false;
        }

        return true;
    }

    private bool IsValidCurrencyDraft(string draft)
    {
        if (string.IsNullOrWhiteSpace(draft))
        {
            return true;
        }

        var d = draft.Trim();
        if (_allowSigned && d.StartsWith("-", StringComparison.Ordinal))
        {
            d = d.Length == 1 ? string.Empty : d[1..];
        }

        if (d.Length == 0)
        {
            return true;
        }

        var dotIdx = d.IndexOf('.');
        if (dotIdx >= 0)
        {
            if (d[(dotIdx + 1)..].Contains('.'))
            {
                return false;
            }

            if (d.Length - dotIdx - 1 > 2)
            {
                return false;
            }
        }

        foreach (var ch in d.Replace(".", string.Empty))
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }
        }

        return d.Length <= 10;
    }

    private decimal ParseCurrencyOrZero() =>
        TryParseCurrency(_draft, out var v) ? v : 0m;

    private static string FormatDecimalPlain(decimal value)
    {
        var abs = Math.Abs(value).ToString("0.00", CultureInfo.InvariantCulture);
        return value < 0m ? "-" + abs : abs;
    }

    private void RaiseDraftChanged()
    {
        OnPropertyChanged(nameof(AmountDisplay));
        DoneCommand.NotifyCanExecuteChanged();
        ToggleSignCommand.NotifyCanExecuteChanged();
    }
}
