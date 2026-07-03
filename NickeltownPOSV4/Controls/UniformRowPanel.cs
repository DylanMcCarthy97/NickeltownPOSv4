using System;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace NickeltownPOSV4.Controls;

/// <summary>Lays out all children in a single horizontal row of equal-width
/// cells with a configurable gap. Used by the Add Drinks category chip strip
/// so chips spread evenly across the products panel.</summary>
public sealed class UniformRowPanel : Panel
{
    public double Spacing { get; set; } = 6;

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Children.Count;
        if (count == 0)
        {
            return new Size(0, 0);
        }

        var availW = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var totalSpacing = Spacing * (count - 1);
        var cellW = Math.Max(0, (availW - totalSpacing) / count);
        var measureH = double.IsFinite(availableSize.Height) ? availableSize.Height : double.PositiveInfinity;
        var cell = new Size(cellW, measureH);

        var maxH = 0d;
        foreach (var child in Children)
        {
            child.Measure(cell);
            if (child.DesiredSize.Height > maxH)
            {
                maxH = child.DesiredSize.Height;
            }
        }

        var w = double.IsPositiveInfinity(availableSize.Width)
            ? cellW * count + totalSpacing
            : availW;
        var h = double.IsPositiveInfinity(availableSize.Height)
            ? maxH
            : Math.Min(availableSize.Height, maxH);
        return new Size(w, h);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = Children.Count;
        if (count == 0)
        {
            return finalSize;
        }

        var totalSpacing = Spacing * (count - 1);
        var cellW = Math.Max(0, (finalSize.Width - totalSpacing) / count);
        var x = 0d;
        for (var i = 0; i < count; i++)
        {
            Children[i].Arrange(new Rect(x, 0, cellW, finalSize.Height));
            x += cellW + Spacing;
        }

        return finalSize;
    }
}
