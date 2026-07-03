using System;

namespace NickeltownPOSV4.ViewModels;

public sealed class PitstopCategoryChipViewModel : ObservableViewModel
{
    private bool _isSelected;

    public PitstopCategoryChipViewModel(string key, string label)
    {
        Key = key;
        Label = label;
        Glyph = ResolveGlyph(label);
    }

    public string Key { get; }

    public string Label { get; }

    /// <summary>Emoji icon shown above the chip label (matches Add Drinks category chips).</summary>
    public string Glyph { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private static string ResolveGlyph(string? label)
    {
        var key = (label ?? string.Empty).Trim();
        if (key.StartsWith("All", StringComparison.OrdinalIgnoreCase))
        {
            return "\u2630";
        }

        if (key.Equals("Food", StringComparison.OrdinalIgnoreCase))
        {
            return "\U0001F354";
        }

        if (key.Equals("Drinks", StringComparison.OrdinalIgnoreCase))
        {
            return "\U0001F964";
        }

        if (key.Equals("Merch", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Merchandise", StringComparison.OrdinalIgnoreCase))
        {
            return "\U0001F455";
        }

        return string.Empty;
    }
}
