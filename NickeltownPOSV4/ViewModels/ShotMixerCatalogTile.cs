using NickeltownPOSV4.Services.AddDrinks;

namespace NickeltownPOSV4.ViewModels;

/// <summary>
/// Shot + Mixer quick-action tile in the Add Drinks catalog grid. Pricing follows the
/// configured Shot + Mixer item (member/guest and specials) via <see cref="ApplyFromConfig"/>.
/// </summary>
public sealed class ShotMixerCatalogTile : ObservableViewModel
{
    private decimal _unitPrice;
    private decimal _regularUnitPrice;
    private bool _showSpecialPricing;

    public string Name => ShotMixerCatalog.ItemName;

    public bool ShowOutOfStockOverlay => false;

    public decimal UnitPrice => _unitPrice;

    public decimal RegularUnitPrice => _regularUnitPrice;

    public bool ShowSpecialPricing => _showSpecialPricing;

    public string PriceText => $"${_unitPrice:0.00}";

    public string RegularPriceText => $"${_regularUnitPrice:0.00}";

    public string SpecialPriceText => $"${_unitPrice:0.00}";

    public void ApplyFromConfig(ShotMixerRuntimeConfig config)
    {
        if (SetProperty(ref _unitPrice, config.ShotPrice))
        {
            OnPropertyChanged(nameof(PriceText));
            OnPropertyChanged(nameof(SpecialPriceText));
        }

        if (SetProperty(ref _regularUnitPrice, config.RegularUnitPrice))
        {
            OnPropertyChanged(nameof(RegularPriceText));
        }

        SetProperty(ref _showSpecialPricing, config.ShowSpecialPricing);
    }
}
