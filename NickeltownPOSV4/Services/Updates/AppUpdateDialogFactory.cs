using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace NickeltownPOSV4.Services.Updates;

/// <summary>
/// Builds the pit-lane themed update dialogs so available / installing / failed / complete
/// all share the same chequered header and copy voice.
/// </summary>
public static class AppUpdateDialogFactory
{
    private const double PanelWidth = 400;
    private const double ChequerCell = 9;

    private static readonly Color HeroTopColor = Color.FromArgb(0xFF, 0x0B, 0x12, 0x20);
    private static readonly Color HeroBottomColor = Color.FromArgb(0xFF, 0x1B, 0x2B, 0x47);

    public static ContentDialog CreateAvailableDialog(XamlRoot xamlRoot, AppUpdateManifest manifest)
    {
        var body = new StackPanel { Spacing = 16, Width = PanelWidth };
        body.Children.Add(BuildHero(
            eyebrow: "Pit stop",
            title: "New build in the pits",
            blurb: $"Version {manifest.Version} is ready to fit. This till is running {AppVersionInfo.CurrentVersionString}.",
            animateChequer: false));

        if (!string.IsNullOrWhiteSpace(manifest.ReleaseNotes))
        {
            body.Children.Add(BuildNotesBlock(manifest.ReleaseNotes.Trim()));
        }

        body.Children.Add(BuildFootnote(
            "Takes about a minute. The app closes and comes back on its own — no need to touch anything."));

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Content = body,
            PrimaryButtonText = "Fit it",
            CloseButtonText = manifest.Mandatory ? string.Empty : "Later",
            DefaultButton = ContentDialogButton.Primary,
        };

        PosContentDialogHelper.ApplyPosStyle(dialog);
        return dialog;
    }

    public static AppUpdateInstallDialog CreateInstallDialog(XamlRoot xamlRoot, string version)
    {
        var body = new StackPanel { Spacing = 18, Width = PanelWidth };
        body.Children.Add(BuildHero(
            eyebrow: "Pit stop in progress",
            title: "Fitting build " + version.Trim(),
            blurb: "Leave this on screen. The till restarts itself when the swap is done.",
            animateChequer: true));

        var stages = new List<StageRow>
        {
            new("1", "Pulling into the pits", "Downloading the new build"),
            new("2", "Fitting the new build", "Installing the package"),
            new("3", "Restarting the till", "Closing and opening again"),
        };

        var stageStack = new StackPanel { Spacing = 12 };
        foreach (var stage in stages)
        {
            stageStack.Children.Add(stage.Root);
        }

        body.Children.Add(stageStack);

        var bar = new ProgressBar
        {
            Height = 6,
            CornerRadius = new CornerRadius(3),
            IsIndeterminate = true,
            Minimum = 0,
            Maximum = 100,
            Foreground = Accent(),
            Background = new SolidColorBrush(Color.FromArgb(0x22, 0x0F, 0x17, 0x2A)),
        };

        var status = new TextBlock
        {
            FontSize = 13,
            Foreground = TextSecondary(),
            TextWrapping = TextWrapping.WrapWholeWords,
            Text = "Getting ready…",
        };

        body.Children.Add(new StackPanel
        {
            Spacing = 8,
            Children = { bar, status },
        });

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Content = body,
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = string.Empty,
        };

        PosContentDialogHelper.ApplyPosStyle(dialog);
        return new AppUpdateInstallDialog(dialog, stages, bar, status);
    }

    public static ContentDialog CreateFailedDialog(XamlRoot xamlRoot, string? errorMessage)
    {
        var body = new StackPanel { Spacing = 16, Width = PanelWidth };
        body.Children.Add(BuildHero(
            eyebrow: "Stopped",
            title: "Didn't finish in the pits",
            blurb: "Nothing changed — the till is still on the build it started with.",
            animateChequer: false));

        body.Children.Add(BuildNotesBlock(
            string.IsNullOrWhiteSpace(errorMessage) ? "Could not install the update." : errorMessage!.Trim(),
            ColorOf("PosBalanceNegativeBrush", Color.FromArgb(0xFF, 0xDC, 0x26, 0x26))));

        body.Children.Add(BuildFootnote(
            "Try again from Admin → Pit updates. If it keeps failing, check the update feed and network."));

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Content = body,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
        };

        PosContentDialogHelper.ApplyPosStyle(dialog);
        return dialog;
    }

    public static ContentDialog CreateCompleteDialog(XamlRoot xamlRoot, string version)
    {
        var body = new StackPanel { Spacing = 16, Width = PanelWidth };
        body.Children.Add(BuildHero(
            eyebrow: "Done",
            title: "Back on track",
            blurb: $"Nickeltown POS is now running version {version.Trim()}.",
            animateChequer: false));

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Content = body,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
        };

        PosContentDialogHelper.ApplyPosStyle(dialog);
        return dialog;
    }

    private static Border BuildHero(string eyebrow, string title, string blurb, bool animateChequer)
    {
        var text = new StackPanel
        {
            Spacing = 6,
            Padding = new Thickness(20, 16, 20, 20),
            Children =
            {
                new TextBlock
                {
                    Text = eyebrow.ToUpperInvariant(),
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    CharacterSpacing = 180,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x93, 0xC5, 0xFD)),
                },
                new TextBlock
                {
                    Text = title,
                    FontSize = 24,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                    TextWrapping = TextWrapping.WrapWholeWords,
                },
                new TextBlock
                {
                    Text = blurb,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1)),
                    TextWrapping = TextWrapping.WrapWholeWords,
                },
            },
        };

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
        };

        var chequer = CreateChequeredStrip(PanelWidth + (ChequerCell * 4), ChequerCell, animateChequer);
        Grid.SetRow(chequer, 0);
        Grid.SetRow(text, 1);
        layout.Children.Add(chequer);
        layout.Children.Add(text);

        var hero = new Border
        {
            CornerRadius = new CornerRadius(14),
            Child = layout,
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = HeroTopColor, Offset = 0 },
                    new GradientStop { Color = HeroBottomColor, Offset = 1 },
                },
            },
        };

        AnimateEntrance(hero);
        return hero;
    }

    /// <summary>
    /// Chequered flag strip drawn from cells so no image asset is needed. Overflow is clipped by
    /// the rounded parent, so <paramref name="width"/> only needs to cover the visible run.
    /// </summary>
    public static FrameworkElement CreateChequeredStrip(double width, double cell = ChequerCell, bool animate = false)
    {
        var columns = (int)Math.Ceiling(Math.Max(width, cell) / cell) + 2;
        var cells = new StackPanel { Orientation = Orientation.Horizontal };
        var light = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9));
        var dark = new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));

        for (var column = 0; column < columns; column++)
        {
            var even = column % 2 == 0;
            cells.Children.Add(new StackPanel
            {
                Children =
                {
                    new Border { Width = cell, Height = cell, Background = even ? light : dark },
                    new Border { Width = cell, Height = cell, Background = even ? dark : light },
                },
            });
        }

        var host = new Grid
        {
            Height = cell * 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { cells },
        };

        if (animate)
        {
            var slide = new TranslateTransform();
            cells.RenderTransform = slide;

            var animation = new DoubleAnimation
            {
                From = 0,
                To = -cell * 2,
                Duration = new Duration(TimeSpan.FromMilliseconds(1100)),
                RepeatBehavior = RepeatBehavior.Forever,
            };
            Storyboard.SetTarget(animation, slide);
            Storyboard.SetTargetProperty(animation, "X");

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            host.Loaded += (_, _) => storyboard.Begin();
        }

        return host;
    }

    private static Border BuildNotesBlock(string text, Color? accent = null)
    {
        return new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush = accent is null ? Accent() : new SolidColorBrush(accent.Value),
            Padding = new Thickness(14, 2, 0, 2),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 14,
                Foreground = TextPrimary(),
                TextWrapping = TextWrapping.WrapWholeWords,
            },
        };
    }

    private static TextBlock BuildFootnote(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = TextSecondary(),
        TextWrapping = TextWrapping.WrapWholeWords,
    };

    private static void AnimateEntrance(FrameworkElement element)
    {
        var offset = new TranslateTransform { Y = 10 };
        element.RenderTransform = offset;
        element.Opacity = 0;

        var duration = new Duration(TimeSpan.FromMilliseconds(260));

        var fade = new DoubleAnimation { From = 0, To = 1, Duration = duration };
        Storyboard.SetTarget(fade, element);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var rise = new DoubleAnimation
        {
            From = 10,
            To = 0,
            Duration = duration,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(rise, offset);
        Storyboard.SetTargetProperty(rise, "Y");

        var storyboard = new Storyboard();
        storyboard.Children.Add(fade);
        storyboard.Children.Add(rise);
        element.Loaded += (_, _) => storyboard.Begin();
    }

    internal static Brush Accent() => Resource("PosAccentBrush", Color.FromArgb(0xFF, 0x25, 0x63, 0xEB));

    internal static Brush Success() => Resource("PosSuccessBrush", Color.FromArgb(0xFF, 0x16, 0xA3, 0x4A));

    internal static Brush TextPrimary() => Resource("PosTextPrimaryBrush", Color.FromArgb(0xFF, 0x0F, 0x17, 0x2A));

    internal static Brush TextSecondary() => Resource("PosTextSecondaryBrush", Color.FromArgb(0xFF, 0x47, 0x55, 0x69));

    private static Brush Resource(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }

    private static Color ColorOf(string key, Color fallback) =>
        Resource(key, fallback) is SolidColorBrush solid ? solid.Color : fallback;

    internal enum StageState
    {
        Waiting,
        Active,
        Done,
    }

    /// <summary>One pit-stop step: numbered marker, label, and a live detail line.</summary>
    internal sealed class StageRow
    {
        private static readonly SolidColorBrush WaitingMarkerBrush = new(Color.FromArgb(0xFF, 0xE2, 0xE8, 0xF0));

        private readonly Border _marker;
        private readonly TextBlock _index;
        private readonly FontIcon _check;
        private readonly TextBlock _title;
        private readonly TextBlock _detail;
        private readonly string _defaultDetail;
        private readonly Storyboard _pulse;

        public StageRow(string index, string title, string defaultDetail)
        {
            _defaultDetail = defaultDetail;

            _index = new TextBlock
            {
                Text = index,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = TextSecondary(),
            };

            _check = new FontIcon
            {
                Glyph = "\uE73E",
                FontSize = 13,
                Visibility = Visibility.Collapsed,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            _marker = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = WaitingMarkerBrush,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new Grid { Children = { _index, _check } },
            };

            _title = new TextBlock
            {
                Text = title,
                FontSize = 15,
                Foreground = TextSecondary(),
                TextWrapping = TextWrapping.WrapWholeWords,
            };

            _detail = new TextBlock
            {
                Text = defaultDetail,
                FontSize = 12,
                Foreground = TextSecondary(),
                Opacity = 0.6,
                TextWrapping = TextWrapping.WrapWholeWords,
            };

            var labels = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(_title);
            labels.Children.Add(_detail);

            var row = new Grid
            {
                ColumnSpacing = 12,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                },
            };

            Grid.SetColumn(_marker, 0);
            Grid.SetColumn(labels, 1);
            row.Children.Add(_marker);
            row.Children.Add(labels);

            Root = row;
            _pulse = BuildPulse(_marker);
        }

        public FrameworkElement Root { get; }

        public void SetState(StageState state, string? detail = null)
        {
            _pulse.Stop();
            _marker.Opacity = 1;
            _detail.Text = string.IsNullOrWhiteSpace(detail) ? _defaultDetail : detail!;

            switch (state)
            {
                case StageState.Active:
                    _marker.Background = Accent();
                    _index.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
                    _index.Visibility = Visibility.Visible;
                    _check.Visibility = Visibility.Collapsed;
                    _title.Foreground = TextPrimary();
                    _title.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                    _detail.Opacity = 1;
                    _pulse.Begin();
                    break;

                case StageState.Done:
                    _marker.Background = Success();
                    _index.Visibility = Visibility.Collapsed;
                    _check.Visibility = Visibility.Visible;
                    _title.Foreground = TextPrimary();
                    _title.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
                    _detail.Opacity = 0.6;
                    break;

                default:
                    _marker.Background = WaitingMarkerBrush;
                    _index.Foreground = TextSecondary();
                    _index.Visibility = Visibility.Visible;
                    _check.Visibility = Visibility.Collapsed;
                    _title.Foreground = TextSecondary();
                    _title.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
                    _detail.Opacity = 0.6;
                    break;
            }
        }

        private static Storyboard BuildPulse(DependencyObject target)
        {
            var animation = new DoubleAnimation
            {
                From = 1,
                To = 0.45,
                Duration = new Duration(TimeSpan.FromMilliseconds(850)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, "Opacity");

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            return storyboard;
        }
    }
}

/// <summary>Wraps the install dialog so progress reports drive the pit-stop stages.</summary>
public sealed class AppUpdateInstallDialog
{
    private readonly List<AppUpdateDialogFactory.StageRow> _stages;
    private readonly ProgressBar _bar;
    private readonly TextBlock _status;

    internal AppUpdateInstallDialog(
        ContentDialog dialog,
        List<AppUpdateDialogFactory.StageRow> stages,
        ProgressBar bar,
        TextBlock status)
    {
        Dialog = dialog;
        _stages = stages;
        _bar = bar;
        _status = status;
    }

    public ContentDialog Dialog { get; }

    public void Report(AppUpdateProgress progress)
    {
        var current = (int)progress.Stage;
        for (var index = 0; index < _stages.Count; index++)
        {
            var state = index < current
                ? AppUpdateDialogFactory.StageState.Done
                : index == current
                    ? AppUpdateDialogFactory.StageState.Active
                    : AppUpdateDialogFactory.StageState.Waiting;

            _stages[index].SetState(state, index == current ? progress.Message : null);
        }

        _status.Text = progress.Percent is double percent
            ? string.Format(CultureInfo.CurrentCulture, "{0} — {1:0}%", progress.Message, percent)
            : progress.Message;

        if (progress.Percent is double value)
        {
            _bar.IsIndeterminate = false;
            _bar.Value = Math.Clamp(value, 0, 100);
        }
        else
        {
            _bar.IsIndeterminate = true;
        }
    }
}
