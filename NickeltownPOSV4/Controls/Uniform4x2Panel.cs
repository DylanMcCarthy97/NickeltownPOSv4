using System;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace NickeltownPOSV4.Controls;

/// <summary>Arranges up to 8 children in a fixed 4x2 grid that fills all available space (tabs board).</summary>
public sealed class Uniform4x2Panel : Panel
{
    private const int Columns = 4;

    private const int Rows = 2;

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var h = double.IsFinite(availableSize.Height) ? availableSize.Height : 0;
        var cellW = w / Columns;
        var cellH = h / Rows;
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

            return new Size(
                double.IsPositiveInfinity(availableSize.Width) ? maxW * Columns : w,
                double.IsPositiveInfinity(availableSize.Height) ? maxH * Rows : h);
        }

        return new Size(w, h);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var fw = double.IsFinite(finalSize.Width) ? finalSize.Width : 0;
        var fh = double.IsFinite(finalSize.Height) ? finalSize.Height : 0;
        var cellW = fw / Columns;
        var cellH = fh / Rows;
        var count = Math.Min(Columns * Rows, Children.Count);

        for (var i = 0; i < count; i++)
        {
            var row = i / Columns;
            var col = i % Columns;
            Children[i].Arrange(new Rect(col * cellW, row * cellH, cellW, cellH));
        }

        return finalSize;
    }
}