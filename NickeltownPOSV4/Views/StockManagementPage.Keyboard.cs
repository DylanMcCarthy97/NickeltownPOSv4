using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace NickeltownPOSV4.Views;

public sealed partial class StockManagementPage
{
    internal sealed class TouchFieldRow
    {
        public required Grid Container { get; init; }
        public required TextBox TextBox { get; init; }
    }

    internal async Task RunWithStockOverlayGateAsync(Func<Task> action)
    {
        if (Interlocked.CompareExchange(ref _stockOverlayGate, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await action().ConfigureAwait(true);
        }
        finally
        {
            Interlocked.Exchange(ref _stockOverlayGate, 0);
        }
    }

    private Button CreateOverlaySideButton(string caption, double minWidth = 118) =>
        new()
        {
            Content = caption,
            MinWidth = minWidth,
            MinHeight = 46,
            VerticalAlignment = VerticalAlignment.Stretch,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"],
        };

    internal TouchFieldRow CreateEditMoneyRow(string text, string overlayTitle) =>
        CreateMoneyRow(text, overlayTitle, buttonCaption: "$", buttonMinWidth: 64);

    internal TouchFieldRow CreateEditIntegerRow(string text, string overlayTitle, int min = 0, int max = int.MaxValue) =>
        CreateIntegerRow(text, overlayTitle, min, max, buttonCaption: "#", buttonMinWidth: 64);

    internal TouchFieldRow CreateKeyboardRow(string initialText, string overlayTitle, string buttonCaption = "Type")
    {
        var tb = CreateOverlayTextBoxShell();
        tb.Text = initialText ?? string.Empty;
        async Task RunOverlayAsync()
        {
            await RunWithStockOverlayGateAsync(async () =>
            {
                var r = await _inputOverlay.ShowKeyboardAsync(tb.Text ?? string.Empty, overlayTitle).ConfigureAwait(true);
                if (r is not null)
                {
                    tb.Text = r;
                }
            }).ConfigureAwait(true);
        }

        var btn = CreateOverlaySideButton(buttonCaption);
        WireOverlayField(tb, btn, RunOverlayAsync);
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(tb, 0);
        Grid.SetColumn(btn, 1);
        grid.Children.Add(tb);
        grid.Children.Add(btn);
        return new TouchFieldRow { Container = grid, TextBox = tb };
    }

    internal TouchFieldRow CreateEditableSearchRow(string initialText, string overlayTitle, string placeholder)
    {
        var tb = CreateEditableTextBoxShell(placeholder);
        tb.Text = initialText ?? string.Empty;
        async Task RunOverlayAsync()
        {
            await RunWithStockOverlayGateAsync(async () =>
            {
                var r = await _inputOverlay.ShowKeyboardAsync(tb.Text ?? string.Empty, overlayTitle).ConfigureAwait(true);
                if (r is not null)
                {
                    tb.Text = r;
                }
            }).ConfigureAwait(true);
        }

        var btn = CreateOverlaySideButton("Keyboard");
        WireOverlayField(tb, btn, RunOverlayAsync);
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(tb, 0);
        Grid.SetColumn(btn, 1);
        grid.Children.Add(tb);
        grid.Children.Add(btn);
        return new TouchFieldRow { Container = grid, TextBox = tb };
    }

    internal Border CreateEditableSearchBar(string placeholder, out TextBox textBox)
    {
        textBox = CreateEditableTextBoxShell(placeholder);
        textBox.BorderThickness = new Thickness(0);
        textBox.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        textBox.MinHeight = 44;

        var icon = new FontIcon
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 16,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["PosTextSecondaryBrush"],
            Glyph = "\uE721",
        };
        var grid = new Grid { Padding = new Thickness(12, 0, 12, 0), ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);
        Grid.SetColumn(textBox, 1);
        grid.Children.Add(textBox);

        return new Border
        {
            MinHeight = 48,
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["PosSurfaceBrush"],
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["PosBorderBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Child = grid,
        };
    }

    internal (Grid Container, TextBlock ValueLabel) CreateLargeCountStepper(
        int initialValue,
        string overlayTitle,
        int min = 0,
        int max = int.MaxValue,
        Action<int>? onValueChanged = null)
    {
        var valueLabel = new TextBlock
        {
            Text = initialValue.ToString(CultureInfo.InvariantCulture),
            FontSize = 44,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 72,
        };

        int CurrentValue() => ParseIntLoose(valueLabel.Text, initialValue);

        void SetValue(int next)
        {
            var clamped = Math.Clamp(next, min, max);
            valueLabel.Text = clamped.ToString(CultureInfo.InvariantCulture);
            onValueChanged?.Invoke(clamped);
        }

        var minus = new Button
        {
            Content = "-",
            MinHeight = 64,
            MinWidth = 72,
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"],
        };
        minus.Click += (_, _) => SetValue(CurrentValue() - 1);

        var plus = new Button
        {
            Content = "+",
            MinHeight = 64,
            MinWidth = 72,
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"],
        };
        plus.Click += (_, _) => SetValue(CurrentValue() + 1);

        async Task RunOverlayAsync()
        {
            await RunWithStockOverlayGateAsync(async () =>
            {
                var init = Math.Clamp(CurrentValue(), min, max);
                var r = await _inputOverlay.ShowIntegerNumpadAsync(init, overlayTitle, min, max).ConfigureAwait(true);
                if (r.HasValue)
                {
                    SetValue(r.Value);
                }
            }).ConfigureAwait(true);
        }

        var keypadBtn = CreateOverlaySideButton("#", 64);
        keypadBtn.MinHeight = 64;
        keypadBtn.Click += async (_, _) => await RunOverlayAsync().ConfigureAwait(true);

        var valueTap = new Button
        {
            MinHeight = 64,
            MinWidth = 96,
            Padding = new Thickness(8, 0, 8, 0),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Content = valueLabel,
        };
        valueTap.Click += async (_, _) => await RunOverlayAsync().ConfigureAwait(true);

        var row = new Grid { ColumnSpacing = 10, HorizontalAlignment = HorizontalAlignment.Center };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(minus, 0);
        row.Children.Add(minus);
        Grid.SetColumn(valueTap, 1);
        row.Children.Add(valueTap);
        Grid.SetColumn(plus, 2);
        row.Children.Add(plus);
        Grid.SetColumn(keypadBtn, 3);
        row.Children.Add(keypadBtn);

        return (row, valueLabel);
    }

    internal TouchFieldRow CreateMoneyRow(
        string text,
        string overlayTitle,
        string buttonCaption = "Enter amount",
        double buttonMinWidth = 118)
    {
        var tb = CreateOverlayTextBoxShell();
        tb.Text = text ?? string.Empty;
        async Task RunOverlayAsync()
        {
            await RunWithStockOverlayGateAsync(async () =>
            {
                var init = ParseDecimalLoose(tb.Text, 0m);
                var r = await _inputOverlay.ShowNumpadAsync(init, overlayTitle, allowSignedAmount: false).ConfigureAwait(true);
                if (r.HasValue)
                {
                    tb.Text = r.Value.ToString("0.00", CultureInfo.InvariantCulture);
                }
            }).ConfigureAwait(true);
        }

        var btn = CreateOverlaySideButton(buttonCaption, buttonMinWidth);
        WireOverlayField(tb, btn, RunOverlayAsync);
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(tb, 0);
        Grid.SetColumn(btn, 1);
        grid.Children.Add(tb);
        grid.Children.Add(btn);
        return new TouchFieldRow { Container = grid, TextBox = tb };
    }

    internal TouchFieldRow CreateIntegerRow(
        string text,
        string overlayTitle,
        int min = 0,
        int max = int.MaxValue,
        string buttonCaption = "Enter number",
        double buttonMinWidth = 118)
    {
        var tb = CreateOverlayTextBoxShell();
        tb.Text = text ?? string.Empty;
        async Task RunOverlayAsync()
        {
            await RunWithStockOverlayGateAsync(async () =>
            {
                var init = ParseIntLoose(tb.Text, min < 0 ? 0 : min);
                if (init < min)
                {
                    init = min;
                }

                if (init > max)
                {
                    init = max;
                }

                var r = await _inputOverlay.ShowIntegerNumpadAsync(init, overlayTitle, min, max).ConfigureAwait(true);
                if (r.HasValue)
                {
                    tb.Text = r.Value.ToString(CultureInfo.InvariantCulture);
                }
            }).ConfigureAwait(true);
        }

        var btn = CreateOverlaySideButton(buttonCaption, buttonMinWidth);
        WireOverlayField(tb, btn, RunOverlayAsync);
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(tb, 0);
        Grid.SetColumn(btn, 1);
        grid.Children.Add(tb);
        grid.Children.Add(btn);
        return new TouchFieldRow { Container = grid, TextBox = tb };
    }

    internal TouchFieldRow CreateSignedDecimalRow(string text, string overlayTitle)
    {
        var tb = CreateOverlayTextBoxShell();
        tb.Text = text ?? string.Empty;
        async Task RunOverlayAsync()
        {
            await RunWithStockOverlayGateAsync(async () =>
            {
                var init = ParseDecimalLoose(tb.Text, 0m);
                var r = await _inputOverlay.ShowNumpadAsync(init, overlayTitle, allowSignedAmount: true).ConfigureAwait(true);
                if (r.HasValue)
                {
                    var s = r.Value.ToString("0.##", CultureInfo.InvariantCulture).TrimEnd('0');
                    if (s.EndsWith(".", StringComparison.Ordinal))
                    {
                        s = s[..^1];
                    }

                    tb.Text = string.IsNullOrEmpty(s) ? "0" : s;
                }
            }).ConfigureAwait(true);
        }

        var btn = CreateOverlaySideButton("Enter value");
        WireOverlayField(tb, btn, RunOverlayAsync);
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(tb, 0);
        Grid.SetColumn(btn, 1);
        grid.Children.Add(tb);
        grid.Children.Add(btn);
        return new TouchFieldRow { Container = grid, TextBox = tb };
    }

    internal static int ParseIntLoose(string? t, int fallback)
    {
        var s = (t ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(s))
        {
            return fallback;
        }

        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
        {
            if (d < 0)
            {
                return fallback;
            }

            if (d != decimal.Truncate(d))
            {
                return fallback;
            }

            if (d > int.MaxValue)
            {
                return fallback;
            }

            return (int)d;
        }

        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : fallback;
    }

    internal static bool TryParseWholeNonNegativeInt(string? text, string fieldLabel, out int value, out string errorMessage)
    {
        value = 0;
        errorMessage = string.Empty;
        var s = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(s))
        {
            errorMessage = $"{fieldLabel} is required.";
            return false;
        }

        if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
        {
            errorMessage = $"{fieldLabel} must be a valid number.";
            return false;
        }

        if (d < 0)
        {
            errorMessage = $"{fieldLabel} must be zero or greater.";
            return false;
        }

        if (d != decimal.Truncate(d))
        {
            errorMessage = $"{fieldLabel} must be a whole number (no decimals).";
            return false;
        }

        if (d > int.MaxValue)
        {
            errorMessage = $"{fieldLabel} is too large.";
            return false;
        }

        value = (int)d;
        return true;
    }

    internal static decimal ParseDecimalLoose(string? t, decimal fallback)
    {
        var s = (t ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(s))
        {
            return fallback;
        }

        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    private static void WireOverlayField(TextBox textBox, Button sideButton, Func<Task> runOverlayAsync)
    {
        sideButton.Click += async (_, _) => await runOverlayAsync().ConfigureAwait(true);
        textBox.PointerPressed += async (_, e) =>
        {
            e.Handled = true;
            await runOverlayAsync().ConfigureAwait(true);
        };
    }

    private TextBox CreateEditableTextBoxShell(string? placeholder = null) =>
        new()
        {
            Style = (Style)Application.Current.Resources["PosTouchFieldTextBoxStyle"],
            IsReadOnly = false,
            PlaceholderText = placeholder ?? string.Empty,
        };

    private TextBox CreateOverlayTextBoxShell() =>
        new()
        {
            Style = (Style)Application.Current.Resources["PosTouchFieldTextBoxStyle"],
            IsReadOnly = true,
        };
}
