using System;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Data.Sqlite;
using Windows.UI;

namespace NickeltownPOSV4.ViewModels;

public sealed class PitstopProductTileViewModel : ObservableViewModel
{
    private readonly Func<long, int> _qtyInCart;

    private readonly Action<PitstopProductTileViewModel> _tap;

    public PitstopProductTileViewModel(
        PitstopCatalogProductRow source,
        Func<long, int> qtyInCart,
        Action<PitstopProductTileViewModel> tap)
    {
        Source = source;
        _qtyInCart = qtyInCart;
        _tap = tap;
        TapCommand = new RelayCommand(() => _tap(this), () => CanTap);
    }

    public PitstopCatalogProductRow Source { get; }

    public IRelayCommand TapCommand { get; }

    public string Name => Source.Name;

    public string CategoryLine
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Source.SubCategoryLabel))
            {
                return $"{Source.CategoryName} · {Source.SubCategoryLabel}";
            }

            return Source.CategoryName;
        }
    }

    public string PriceLine => $"${Source.EffectivePriceText}";

    public bool ShowSpecialPricing =>
        CatalogSpecialPricing.ShouldShowPitstopSpecialPrice(
            Source.IsOnSpecial,
            Source.PitstopPrice,
            Source.PitstopSpecialPrice,
            out _,
            out _);

    public string RegularPriceText
    {
        get
        {
            if (CatalogSpecialPricing.ShouldShowPitstopSpecialPrice(
                    Source.IsOnSpecial,
                    Source.PitstopPrice,
                    Source.PitstopSpecialPrice,
                    out var regular,
                    out _))
            {
                return $"${regular.ToString("0.00", CultureInfo.InvariantCulture)}";
            }

            return $"${((decimal)Source.PitstopPrice).ToString("0.00", CultureInfo.InvariantCulture)}";
        }
    }

    public string SpecialPriceText => $"${Source.EffectivePriceText}";

    public bool ShowSpecialBadge => ShowSpecialPricing;

    public bool IsOrderIn => Source.OrderInMerchandise != 0;

    public bool TracksStock => Source.TrackStock != 0;

    public int DisplayedStock
    {
        get
        {
            if (IsOrderIn || !TracksStock)
            {
                return Source.StockQty;
            }

            var reserved = _qtyInCart(Source.ItemId);
            return Math.Max(0, Source.StockQty - reserved);
        }
    }

    public bool ShowStockLabel => TracksStock || IsOrderIn;

    public string StockLabel
    {
        get
        {
            if (IsOrderIn)
            {
                return "Order-in";
            }

            if (!TracksStock)
            {
                return string.Empty;
            }

            return $"Stock {DisplayedStock}";
        }
    }

    public bool IsOutOfStock => !IsOrderIn && TracksStock && DisplayedStock <= 0;

    public bool IsLowStock =>
        !IsOrderIn && TracksStock && DisplayedStock > 0 && DisplayedStock <= Source.EffectiveLowStockThreshold;

    public bool CanTap => !IsOutOfStock || IsOrderIn || !TracksStock;

    public bool ShowOutOfStockOverlay => IsOutOfStock;

    /// <summary>Matches Add Drinks catalog cards (0.55 when out of stock).</summary>
    public double CardOpacity => CanTap ? 1.0 : 0.55;

    public Brush AccentStripBrush
    {
        get
        {
            if (UseOutOfStockChrome)
            {
                return ThemeBrush("PosBalanceSettledBrush");
            }

            if (UseOrderInChrome || ShowSpecialBadge)
            {
                return ThemeBrush("PosTabStripGuestBrush");
            }

            if (UseLowStockChrome)
            {
                return ThemeBrush("PosBalanceLowBrush");
            }

            return ThemeBrush("PosBalanceGoodBrush");
        }
    }

    public Brush StockForegroundBrush
    {
        get
        {
            if (UseOutOfStockChrome)
            {
                return ThemeBrush("PosBalanceNegativeBrush");
            }

            if (UseLowStockChrome)
            {
                return ThemeBrush("PosBalanceLowBrush");
            }

            return ThemeBrush("PosBalanceGoodBrush");
        }
    }

    /// <summary>V2 Pitstop tile chrome: order-in purple border, low-stock warning, out-of-stock muted.</summary>
    public bool UseOrderInChrome => IsOrderIn;

    public bool UseLowStockChrome => IsLowStock;

    public bool UseOutOfStockChrome => IsOutOfStock && !IsOrderIn && TracksStock;

    public bool UseNoStockTrackChrome => !TracksStock && !IsOrderIn;

    public Brush TileBorderBrush
    {
        get
        {
            if (UseOutOfStockChrome)
            {
                return new SolidColorBrush(Color.FromArgb(255, 203, 213, 225));
            }

            if (UseOrderInChrome)
            {
                return new SolidColorBrush(Color.FromArgb(255, 168, 85, 247));
            }

            if (UseLowStockChrome)
            {
                return new SolidColorBrush(Color.FromArgb(255, 245, 158, 11));
            }

            if (UseNoStockTrackChrome)
            {
                return new SolidColorBrush(Color.FromArgb(255, 59, 130, 246));
            }

            return new SolidColorBrush(Color.FromArgb(255, 226, 232, 240));
        }
    }

    public string? SkuSmall => string.IsNullOrWhiteSpace(Source.Sku) ? null : Source.Sku.Trim();

    public bool HasSkuLine => !string.IsNullOrWhiteSpace(SkuSmall);

    public void RefreshCartBinding()
    {
        OnPropertyChanged(nameof(DisplayedStock));
        OnPropertyChanged(nameof(StockLabel));
        OnPropertyChanged(nameof(ShowStockLabel));
        OnPropertyChanged(nameof(IsOutOfStock));
        OnPropertyChanged(nameof(IsLowStock));
        OnPropertyChanged(nameof(CanTap));
        OnPropertyChanged(nameof(CardOpacity));
        OnPropertyChanged(nameof(ShowOutOfStockOverlay));
        OnPropertyChanged(nameof(AccentStripBrush));
        OnPropertyChanged(nameof(StockForegroundBrush));
        TapCommand.NotifyCanExecuteChanged();
    }

    private static Brush ThemeBrush(string key)
    {
        if (Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.FromArgb(255, 148, 163, 184));
    }
}
