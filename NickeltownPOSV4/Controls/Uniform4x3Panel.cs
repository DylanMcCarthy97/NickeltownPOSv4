using System;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace NickeltownPOSV4.Controls;

/// <summary>Arranges children in a 4x3 grid that fills all available space (Pitstop product board).</summary>
public sealed class Uniform4x3Panel : Panel
{
    public const int ColumnCount = 4;

    public const int RowCount = 3;

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var h = double.IsFinite(availableSize.Height) ? availableSize.Height : 0;
        var cell = GetCellSize(w, h);

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
                double.IsPositiveInfinity(availableSize.Width) ? maxW * ColumnCount : w,
                double.IsPositiveInfinity(availableSize.Height) ? maxH * RowCount : h);
        }

        return new Size(w, h);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var cell = GetCellSize(finalSize.Width, finalSize.Height);
        var count = Math.Min(ColumnCount * RowCount, Children.Count);

        for (var i = 0; i < count; i++)
        {
            var row = i / ColumnCount;
            var col = i % ColumnCount;
            var x = col * cell.Width;
            var y = row * cell.Height;
            Children[i].Arrange(new Rect(x, y, cell.Width, cell.Height));
        }

        return finalSize;
    }

    private static Size GetCellSize(double width, double height)
    {
        var w = double.IsFinite(width) ? Math.Max(0, width) : 0;
        var h = double.IsFinite(height) ? Math.Max(0, height) : 0;
        return new Size(w / ColumnCount, h / RowCount);
    }
}