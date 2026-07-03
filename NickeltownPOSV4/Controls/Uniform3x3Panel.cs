using System;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace NickeltownPOSV4.Controls;

/// <summary>Arranges up to 9 children in a fixed 3×3 grid that fills all available space (for tabs board).</summary>
public sealed class Uniform3x3Panel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var h = double.IsFinite(availableSize.Height) ? availableSize.Height : 0;
        var cellW = w / 3d;
        var cellH = h / 3d;
        var cell = new Size(Math.Max(0, cellW), Math.Max(0, cellH));

        foreach (var child in Children)
        {
            child.Measure(cell);
        }

        if (double.IsPositiveInfinity(availableSize.Width) || double.IsPositiveInfinity(availableSize.Height))
        {
            var maxW = 0d;
            var maxH = 0d;
            foreach (var child in Children)
            {
                maxW = Math.Max(maxW, child.DesiredSize.Width);
                maxH = Math.Max(maxH, child.DesiredSize.Height);
            }

            var desiredW = maxW * 3d;
            var desiredH = maxH * 3d;
            return new Size(
                double.IsPositiveInfinity(availableSize.Width) ? desiredW : w,
                double.IsPositiveInfinity(availableSize.Height) ? desiredH : h);
        }

        return new Size(w, h);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var fw = double.IsFinite(finalSize.Width) ? finalSize.Width : 0;
        var fh = double.IsFinite(finalSize.Height) ? finalSize.Height : 0;
        var w = fw / 3d;
        var h = fh / 3d;
        var count = Math.Min(9, Children.Count);

        for (var i = 0; i < count; i++)
        {
            var row = i / 3;
            var col = i % 3;
            var x = col * w;
            var y = row * h;
            Children[i].Arrange(new Rect(x, y, w, h));
        }

        return finalSize;
    }
}
