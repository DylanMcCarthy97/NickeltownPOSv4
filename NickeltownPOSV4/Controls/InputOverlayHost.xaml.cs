using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

namespace NickeltownPOSV4.Controls;

public sealed partial class InputOverlayHost : UserControl
{
    private bool _isOpen;
    private bool _animating;

    public InputOverlayHost()
    {
        InitializeComponent();
        IsHitTestVisible = false;
        DimOverlay.Opacity = 0;
    }

    public bool IsOpen => _isOpen;

    public event EventHandler? BackgroundDismissed;

    public void Open(UserControl content)
    {
        if (_animating)
        {
            return;
        }

        Presenter.Content = content;
        IsHitTestVisible = true;
        _isOpen = true;
        RunOpenAnimation();
    }

    public void Close()
    {
        if (!_isOpen || _animating)
        {
            return;
        }

        RunCloseAnimation();
    }

    private void RunOpenAnimation()
    {
        _animating = true;
        var sb = new Storyboard();
        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(140),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(fade, DimOverlay);
        Storyboard.SetTargetProperty(fade, "Opacity");
        sb.Children.Add(fade);
        sb.Completed += (_, _) => _animating = false;
        sb.Begin();
    }

    private void RunCloseAnimation()
    {
        _animating = true;
        var sb = new Storyboard();
        var fade = new DoubleAnimation
        {
            From = DimOverlay.Opacity,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(120),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        Storyboard.SetTarget(fade, DimOverlay);
        Storyboard.SetTargetProperty(fade, "Opacity");
        sb.Children.Add(fade);
        sb.Completed += (_, _) =>
        {
            Presenter.Content = null;
            _isOpen = false;
            IsHitTestVisible = false;
            _animating = false;
        };
        sb.Begin();
    }

    private void DimOverlay_Tapped(object sender, TappedRoutedEventArgs e) =>
        BackgroundDismissed?.Invoke(this, EventArgs.Empty);

    private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!_isOpen)
        {
            return;
        }

        args.Handled = true;
        BackgroundDismissed?.Invoke(this, EventArgs.Empty);
    }
}
