using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

namespace NickeltownPOSV4.Controls;

public sealed partial class SlidePanelHost : UserControl
{
    private const double DefaultPanelWidth = 488;

    private double _chromeWidth = DefaultPanelWidth;

    private bool _isOpen;

    private bool _animating;

    private Storyboard? _activeStoryboard;

    /// <summary>Bumped when slide content is replaced without finishing a close animation — stale close completions must not clear the panel.</summary>
    private int _contentEpoch;

    public SlidePanelHost()
    {
        InitializeComponent();
        ApplyChromeWidth(DefaultPanelWidth);
        DimOverlay.Opacity = 0;
        DimOverlay.IsHitTestVisible = false;
        // Prevent WinUI from painting a floating "Esc" hint for the slide-panel accelerator.
        foreach (var accelerator in KeyboardAccelerators)
        {
            accelerator.IsEnabled = false;
        }
    }

    public bool IsPanelOpen => _isOpen;

    /// <summary>Opens the panel. Pass <paramref name="panelWidthPixels"/> to widen (e.g. Add Drinks).</summary>
    public void Open(UserControl content, double? panelWidthPixels = null)
    {
        var w = ResolveWidth(panelWidthPixels);
        _chromeWidth = w;
        ApplyChromeWidth(w);

        // Replace slide content while the shell is already showing (e.g. More actions → Edit tab).
        // Must run before the `_animating` guard: a prior Close() may still be animating.
        if (_isOpen)
        {
            StopActiveAnimation();
            _contentEpoch++;
            PanelSlideTransform.X = 0;
            SetEscapeAcceleratorEnabled(true);
            Presenter.Content = content;
            DimOverlay.Opacity = 0.52;
            DimOverlay.IsHitTestVisible = true;
            IsHitTestVisible = true;
            _animating = false;
            return;
        }

        if (_animating)
        {
            StopActiveAnimation();
            _animating = false;
        }

        Presenter.Content = content;
        _isOpen = true;
        SetEscapeAcceleratorEnabled(true);
        IsHitTestVisible = true;
        DimOverlay.IsHitTestVisible = true;
        DimOverlay.Opacity = 0;
        PanelSlideTransform.X = _chromeWidth;
        _animating = true;
        RunOpenAnimation(() => _animating = false);
    }

    public void Close()
    {
        if (!_isOpen)
        {
            return;
        }

        if (_animating)
        {
            StopActiveAnimation();
        }

        var closeEpoch = _contentEpoch;
        _animating = true;
        RunCloseAnimation(() =>
        {
            if (_contentEpoch != closeEpoch)
            {
                return;
            }

            Presenter.Content = null;
            _isOpen = false;
            SetEscapeAcceleratorEnabled(false);
            DimOverlay.IsHitTestVisible = false;
            DimOverlay.Opacity = 0;
            IsHitTestVisible = false;
            _animating = false;
            _chromeWidth = DefaultPanelWidth;
            ApplyChromeWidth(DefaultPanelWidth);
        });
    }

    private static double ResolveWidth(double? panelWidthPixels)
    {
        if (panelWidthPixels is not { } v)
        {
            return DefaultPanelWidth;
        }

        // TCxWave 1024px: allow near-full-width workspace overlays (e.g. Add Drinks).
        if (double.IsNaN(v) || v < 320 || v > 1024)
        {
            return DefaultPanelWidth;
        }

        return v;
    }

    private void ApplyChromeWidth(double w)
    {
        PanelChrome.Width = w;
        if (!_isOpen && !_animating)
        {
            PanelSlideTransform.X = w;
        }
    }

    private void StopActiveAnimation()
    {
        if (_activeStoryboard is null)
        {
            return;
        }

        _activeStoryboard.Stop();
        _activeStoryboard = null;
        _animating = false;
    }

    private void RunOpenAnimation(Action onComplete)
    {
        StopActiveAnimation();
        var sb = new Storyboard();

        var slide = new DoubleAnimation
        {
            From = _chromeWidth,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(280),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(slide, PanelSlideTransform);
        Storyboard.SetTargetProperty(slide, "X");
        sb.Children.Add(slide);

        var fade = new DoubleAnimation
        {
            From = 0,
            To = 0.52,
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(fade, DimOverlay);
        Storyboard.SetTargetProperty(fade, "Opacity");
        sb.Children.Add(fade);

        void Completed(object? s, object e)
        {
            sb.Completed -= Completed;
            if (ReferenceEquals(_activeStoryboard, sb))
            {
                _activeStoryboard = null;
            }

            onComplete();
        }

        _activeStoryboard = sb;
        sb.Completed += Completed;
        sb.Begin();
    }

    private void RunCloseAnimation(Action onComplete)
    {
        StopActiveAnimation();
        var sb = new Storyboard();

        var slide = new DoubleAnimation
        {
            From = PanelSlideTransform.X,
            To = _chromeWidth,
            Duration = TimeSpan.FromMilliseconds(260),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        Storyboard.SetTarget(slide, PanelSlideTransform);
        Storyboard.SetTargetProperty(slide, "X");
        sb.Children.Add(slide);

        var fade = new DoubleAnimation
        {
            From = DimOverlay.Opacity,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        Storyboard.SetTarget(fade, DimOverlay);
        Storyboard.SetTargetProperty(fade, "Opacity");
        sb.Children.Add(fade);

        void Completed(object? s, object e)
        {
            sb.Completed -= Completed;
            if (ReferenceEquals(_activeStoryboard, sb))
            {
                _activeStoryboard = null;
            }

            onComplete();
        }

        _activeStoryboard = sb;
        sb.Completed += Completed;
        sb.Begin();
    }

    private void SetEscapeAcceleratorEnabled(bool enabled)
    {
        foreach (var accelerator in KeyboardAccelerators)
        {
            accelerator.IsEnabled = enabled;
        }
    }

    private void DimOverlay_Tapped(object sender, TappedRoutedEventArgs e) => Close();

    private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!_isOpen)
        {
            return;
        }

        Close();
        args.Handled = true;
    }
}
