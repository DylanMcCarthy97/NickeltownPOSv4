using System;

namespace NickeltownPOSV4.ViewModels;

public sealed class CategoryChipItem : ObservableViewModel
{
    private bool _isSelected;

    public CategoryChipItem(string label)
    {
        Label = label;
        DisplayLabel = label;
        Glyph = ResolveGlyph(label);
    }

    /// <summary>Category filter key (All, Favourites, Beer, …).</summary>
    public string Label { get; }

    public string DisplayLabel { get; }

    /// <summary>Compact icon shown above the label in the toolbar chip.</summary>
    public string Glyph { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public override string ToString() => DisplayLabel;

    private static string ResolveGlyph(string? label)
    {
        var key = (label ?? string.Empty).Trim();
        if (key.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            return "\u2630"; // ☰ (hamburger / list)
        }

        if (key.Equals("Favourites", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Favorites", StringComparison.OrdinalIgnoreCase))
        {
            return "\u2B50"; // ⭐
        }

        if (key.Equals("Beer", StringComparison.OrdinalIgnoreCase))
        {
            return "\U0001F37A"; // 🍺
        }

        if (key.Equals("Drinks", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Soft Drinks", StringComparison.OrdinalIgnoreCase))
        {
            return "\U0001F964"; // 🥤
        }

        if (key.Equals("Food", StringComparison.OrdinalIgnoreCase))
        {
            return "\U0001F354"; // 🍔
        }

        if (key.Equals("Merch", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Merchandise", StringComparison.OrdinalIgnoreCase))
        {
            return "\U0001F455"; // 👕
        }

        if (key.Equals("Spirits", StringComparison.OrdinalIgnoreCase))
        {
            return "\U0001F943"; // 🥃
        }

        if (key.Equals("Wine", StringComparison.OrdinalIgnoreCase))
        {
            return "\U0001F377"; // 🍷
        }

        return string.Empty;
    }
}
