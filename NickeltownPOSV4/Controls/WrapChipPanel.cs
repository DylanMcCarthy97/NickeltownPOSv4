using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace NickeltownPOSV4.Controls;

/// <summary>Wraps children left-to-right with configurable gaps (filter chips).</summary>
public sealed class WrapChipPanel : Panel
{
    public double HorizontalSpacing { get; set; } = 8;

    public double VerticalSpacing { get; set; } = 6;

    protected override Size MeasureOverride(Size availableSize)
    {
        var availW = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        if (availW <= 0)
        {
            availW = double.PositiveInfinity;
        }

        var x = 0d;
        var y = 0d;
        var rowH = 0d;

        foreach (var child in Children)
        {
            child.Measure(new Size(availW, double.PositiveInfinity));
            var cw = child.DesiredSize.Width;
            var ch = child.DesiredSize.Height;

            if (x > 0 && x + cw > availW)
            {
                x = 0;
                y += rowH + VerticalSpacing;
                rowH = 0;
            }

            x += cw + HorizontalSpacing;
            if (ch > rowH)
            {
                rowH = ch;
            }
        }

        var totalH = Children.Count == 0 ? 0 : y + rowH;
        var totalW = double.IsPositiveInfinity(availableSize.Width)
            ? Math.Max(0, x - HorizontalSpacing)
            : availW;
        return new Size(totalW, totalH);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0d;
        var y = 0d;
        var rowH = 0d;

        foreach (var child in Children)
        {
            var cw = child.DesiredSize.Width;
            var ch = child.DesiredSize.Height;

            if (x > 0 && x + cw > finalSize.Width)
            {
                x = 0;
                y += rowH + VerticalSpacing;
                rowH = 0;
            }

            child.Arrange(new Rect(x, y, cw, ch));
            x += cw + HorizontalSpacing;
            if (ch > rowH)
            {
                rowH = ch;
            }
        }

        return finalSize;
    }
}