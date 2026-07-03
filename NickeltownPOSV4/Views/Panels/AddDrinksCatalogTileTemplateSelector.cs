using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views.Panels;

/// <summary>
/// Picks the Shot + Mixer tile template vs the standard product tile template
/// while keeping both inside the same ItemsWrapGrid (identical cell sizing,
/// margins and pagination behaviour).
/// </summary>
public sealed class AddDrinksCatalogTileTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ProductTemplate { get; set; }

    public DataTemplate? ShotMixerTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item) =>
        item is ShotMixerCatalogTile ? ShotMixerTemplate! : ProductTemplate!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) =>
        SelectTemplateCore(item);
}