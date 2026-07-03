using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.AddDrinks;
using NickeltownPOSV4.Services.Stock;

namespace NickeltownPOSV4.ViewModels;

/// <summary>Combo row for legacy percent/fixed special metadata on Items.</summary>
public sealed class SpecialTypeListItem
{
    public string Value { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;
}

/// <summary>Fixed-layout stock admin (1024×768): filter cards, scrollable list, full-screen sub-flows.</summary>
public sealed class StockManagementPageViewModel : ObservableViewModel
{
    public const int DefaultFilterIndex = StockManagementBrowserFilter.ChipAll;

    private readonly IStockEditingService _stock;

    private readonly StockItemAdminPersistenceService _persist;

    private readonly IInputOverlayService _input;

    private readonly IBarCatalogCache _barCatalogCache;

    private readonly IShotMixerConfigService _shotMixerConfig;

    private readonly ITabWorkspaceRefreshBus _refreshBus;

    private readonly IStockProductImageStorage _productImages;

    private List<StockEditorRow> _all = new();

    public const long FilterAllCategoriesId = -999;

    private string _searchText = string.Empty;

    private string _filterCatalogLabel = "All";

    private long? _selectedItemId;

    private long? _newItemDraftId;

    private StockListRowViewModel? _selectedPageRow;

    private string _detailName = string.Empty;

    private string _detailItemType = "Item";

    private string _detailShotMixerSpiritsText = string.Empty;

    private string _detailSku = string.Empty;

    private string _detailImagePath = string.Empty;

    private string _detailStockText = "0";

    private bool _detailTrackStock = true;

    private bool _detailIsActive = true;

    private string _detailCatalogBucket = StockCatalogTaxonomy.BucketBar;

    private string _detailCatalogSubCategory = "Drinks";

    private string _detailStockMode = StockCatalogTaxonomy.StockModeTracked;

    private string _detailCatalogDisplay = string.Empty;

    private string _detailGuestPriceText = string.Empty;

    private string _detailBarSpecialText = string.Empty;

    private string _detailGuestSpecialText = string.Empty;

    private string _detailPitstopSpecialText = string.Empty;

    private string _detailAlternateSkusText = string.Empty;

    private string _detailItemDescription = string.Empty;

    private bool _detailIsOnSpecial;

    private bool _showInBar;

    private bool _showInPitstop;

    private bool _detailOrderInMerchandise;

    private string _statusMessage = string.Empty;

    private bool _isBusy;

    private int _browserFilterIndex = DefaultFilterIndex;

    private StockManagementScreen _currentScreen = StockManagementScreen.Home;

    private string _detailParLevelText = string.Empty;

    private string _detailPreferredStockLevelText = string.Empty;

    private string _detailWarnMeBelowText = string.Empty;

    private string _detailPurchaseUnitQtyText = string.Empty;

    private bool _detailShowOnShoppingList = true;

    private string _lastStockCountDisplay = "—";

    private string _estimatedShoppingSpendText = "$0.00";

    private int _summaryNeedBuyingCount;

    private int _summaryOutOfStockCount;

    private int _summaryTotalItemsCount;

    private string _detailNotesText = string.Empty;

    private long? _detailMixerItemId;

    private int _detailMixerQty = 1;

    private int _adjustQuantity = 1;

    private string _adjustMode = "Increase";

    private string _adjustReason = "Manual adjustment";

    private string _barPriceText = string.Empty;

    private string _pitstopPriceText = string.Empty;

    private string _costPriceText = string.Empty;

    private string _lowStockThresholdText = string.Empty;

    private bool _detailUsesOpenPrice;

    private bool _specialEnabled;

    private string _specialLabel = string.Empty;

    /// <summary>FixedPrice or PercentOff.</summary>
    private string _specialType = "FixedPrice";

    private string _specialValueText = string.Empty;

    private string _specialAppliesToMode = "Bar";

    private string _stockAdjustmentText = string.Empty;

    private string _stockAdjustmentReason = string.Empty;

    public StockManagementPageViewModel(
        IStockEditingService stock,
        StockItemAdminPersistenceService persist,
        IInputOverlayService input,
        IBarCatalogCache barCatalogCache,
        IShotMixerConfigService shotMixerConfig,
        ITabWorkspaceRefreshBus refreshBus,
        IStockProductImageStorage productImages)
    {
        _stock = stock;
        _persist = persist;
        _input = input;
        _barCatalogCache = barCatalogCache;
        _shotMixerConfig = shotMixerConfig;
        _refreshBus = refreshBus;
        _productImages = productImages;
        SpecialApplyToItems.Add("Bar");
        SpecialApplyToItems.Add("Pitstop");
        SpecialApplyToItems.Add("Both");
        SpecialTypeOptionItems.Add(new SpecialTypeListItem { Value = "FixedPrice", Label = "Fixed price" });
        SpecialTypeOptionItems.Add(new SpecialTypeListItem { Value = "PercentOff", Label = "Percentage off" });
        FilterCards.Add(new StockFilterCardViewModel(StockManagementBrowserFilter.ChipAll, "All", "\uE8FD", StockFilterCardViewModel.AccentForFilter(StockManagementBrowserFilter.ChipAll)));
        FilterCards.Add(new StockFilterCardViewModel(StockManagementBrowserFilter.ChipNeedBuying, "Need Buying", "\uE946", StockFilterCardViewModel.AccentForFilter(StockManagementBrowserFilter.ChipNeedBuying)));
        FilterCards.Add(new StockFilterCardViewModel(StockManagementBrowserFilter.ChipOutOfStock, "Out", "\uE7BA", StockFilterCardViewModel.AccentForFilter(StockManagementBrowserFilter.ChipOutOfStock)));
        FilterCards.Add(new StockFilterCardViewModel(StockManagementBrowserFilter.ChipDrinks, "Drinks", "\uE8F4", StockFilterCardViewModel.AccentForFilter(StockManagementBrowserFilter.ChipDrinks)));
        FilterCards.Add(new StockFilterCardViewModel(StockManagementBrowserFilter.ChipMerch, "Merch", "\uE8F1", StockFilterCardViewModel.AccentForFilter(StockManagementBrowserFilter.ChipMerch)));
        FilterCards.Add(new StockFilterCardViewModel(StockManagementBrowserFilter.ChipInactive, "Inactive", "\uE7BA", StockFilterCardViewModel.AccentForFilter(StockManagementBrowserFilter.ChipInactive)));
        SelectFilterCardCommand = new RelayCommand<int>(SelectFilterCard);
        OpenItemEditCommand = new RelayCommand(OpenItemEditFromSelection, () => HasSelection);
        NavigateHomeCommand = new RelayCommand(() => CurrentScreen = StockManagementScreen.Home);
        RefreshCommand = new AsyncRelayCommand(async () => await LoadAsync());
        SaveItemSectionCommand = new AsyncRelayCommand(() => PersistFullItemAsync(null), CanSaveDetail);
        SavePricesCommand = new AsyncRelayCommand(() => PersistFullItemAsync(null), CanSaveDetail);
        SaveStockSettingsCommand = new AsyncRelayCommand(
            () => PersistFullItemAsync(
                string.IsNullOrWhiteSpace(StockAdjustmentReason) ? null : StockAdjustmentReason.Trim()),
            CanSaveDetail);
        SaveBarcodeCommand = new AsyncRelayCommand(() => PersistFullItemAsync(null), CanSaveDetail);
        SelectRowCommand = new RelayCommand<StockListRowViewModel?>(r => SelectedPageRow = r);
        SelectBrowserFilterCommand = new RelayCommand<string>(SelectBrowserFilterFromParameter);
        StockDeltaCommand = new RelayCommand<string>(ApplyStockDeltaFromParameter, _ => HasSelection && !IsBusy);
        SummaryQuickStockCommand = new AsyncRelayCommand<string>(
            ApplyQuickStockDeltaAndPersistAsync,
            _ => CanSummaryQuickStock());
        ApplyStockAdjustmentCommand = new RelayCommand(ApplyStockAdjustmentFromText, () => HasSelection);
        ClearBarcodeCommand = new RelayCommand(ClearBarcode, () => HasSelection);
        ScanBarcodePlaceholderCommand = new AsyncRelayCommand(ScanBarcodeAsync, () => HasSelection && !IsBusy);
    }

    public ObservableCollection<StockListRowViewModel> PageRows { get; } = new();

    public ObservableCollection<StockFilterCardViewModel> FilterCards { get; } = new();

    public ObservableCollection<string> CatalogFilterOptions { get; } = new();

    public string ItemCountText => $"{PageRows.Count} Item{(PageRows.Count == 1 ? string.Empty : "s")}";

    public int NeedBuyingCount => _all.Count(StockInventoryLevelHelper.NeedsBuying);

    public string NeedBuyingBadgeText =>
        NeedBuyingCount > 0 ? NeedBuyingCount.ToString(CultureInfo.InvariantCulture) : string.Empty;

    public bool ShowNeedBuyingBadge => NeedBuyingCount > 0;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RebindPage();
            }
        }
    }

    public string FilterCatalogLabel
    {
        get => _filterCatalogLabel;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? "All" : value.Trim();
            if (SetProperty(ref _filterCatalogLabel, v))
            {
                RebindPage();
            }
        }
    }

    public StockManagementScreen CurrentScreen
    {
        get => _currentScreen;
        set
        {
            if (SetProperty(ref _currentScreen, value))
            {
                OnPropertyChanged(nameof(IsHomeScreen));
                OnPropertyChanged(nameof(IsFullScreenActive));
                OnPropertyChanged(nameof(IsItemEditScreen));
                OnPropertyChanged(nameof(IsAdjustStockScreen));
                OnPropertyChanged(nameof(IsImportWizardScreen));
                OnPropertyChanged(nameof(IsShoppingListScreen));
                OnPropertyChanged(nameof(IsReceiveStockScreen));
                OnPropertyChanged(nameof(IsCountStockScreen));
                OnPropertyChanged(nameof(IsProductsScreen));
            }
        }
    }

    public bool IsHomeScreen => CurrentScreen == StockManagementScreen.Home;

    public bool IsFullScreenActive => CurrentScreen != StockManagementScreen.Home;

    public bool IsItemEditScreen => CurrentScreen == StockManagementScreen.ItemEdit;

    public bool IsAdjustStockScreen => CurrentScreen == StockManagementScreen.AdjustStock;

    public bool IsImportWizardScreen => CurrentScreen == StockManagementScreen.ImportWizard;

    public bool IsShoppingListScreen => CurrentScreen == StockManagementScreen.ShoppingList;

    public bool IsReceiveStockScreen => CurrentScreen == StockManagementScreen.ReceiveStock;

    public bool IsCountStockScreen => CurrentScreen == StockManagementScreen.CountStock;

    public bool IsProductsScreen => CurrentScreen == StockManagementScreen.Products;

    public string SummaryNeedBuyingText => _summaryNeedBuyingCount.ToString(CultureInfo.InvariantCulture);

    public string SummaryOutOfStockText => _summaryOutOfStockCount.ToString(CultureInfo.InvariantCulture);

    public string SummaryTotalItemsText => _summaryTotalItemsCount.ToString(CultureInfo.InvariantCulture);

    public string LastStockCountDisplay => _lastStockCountDisplay;

    public string EstimatedShoppingSpendText => _estimatedShoppingSpendText;

    public string DetailPreferredStockLevelText
    {
        get => _detailPreferredStockLevelText;
        set => SetProperty(ref _detailPreferredStockLevelText, value);
    }

    public string DetailWarnMeBelowText
    {
        get => _detailWarnMeBelowText;
        set => SetProperty(ref _detailWarnMeBelowText, value);
    }

    public string DetailPurchaseUnitQtyText
    {
        get => _detailPurchaseUnitQtyText;
        set => SetProperty(ref _detailPurchaseUnitQtyText, value);
    }

    public bool DetailShowOnShoppingList
    {
        get => _detailShowOnShoppingList;
        set => SetProperty(ref _detailShowOnShoppingList, value);
    }

    public int BrowserFilterIndex
    {
        get => _browserFilterIndex;
        set
        {
            var v = Math.Clamp(value, StockManagementBrowserFilter.ChipAll, StockManagementBrowserFilter.ChipInactive);
            if (SetProperty(ref _browserFilterIndex, v))
            {
                RefreshFilterCardSelection();
                RebindPage();
            }
        }
    }

    public string AdjustQuantityText
    {
        get => _adjustQuantity.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse((value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var q))
            {
                _adjustQuantity = Math.Max(0, q);
                OnPropertyChanged(nameof(AdjustQuantityText));
            }
        }
    }

    public string AdjustMode
    {
        get => _adjustMode;
        set => SetProperty(ref _adjustMode, value);
    }

    public string AdjustReason
    {
        get => _adjustReason;
        set => SetProperty(ref _adjustReason, value);
    }

    public string DetailParLevelText
    {
        get => _detailParLevelText;
        set => SetProperty(ref _detailParLevelText, value);
    }

    public string DetailNotesText
    {
        get => _detailNotesText;
        set => SetProperty(ref _detailNotesText, value);
    }

    public long? DetailMixerItemId
    {
        get => _detailMixerItemId;
        set => SetProperty(ref _detailMixerItemId, value);
    }

    public int DetailMixerQty
    {
        get => _detailMixerQty;
        set => SetProperty(ref _detailMixerQty, Math.Max(1, value));
    }

    public string StockStatusPreview
    {
        get
        {
            if (!HasSelection)
            {
                return "—";
            }

            if (!HasSelection || !int.TryParse(DetailStockText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty))
            {
                return "—";
            }

            var row = BuildPreviewRowFromDetail(qty);
            return StockInventoryLevelHelper.StatusDisplayText(row);
        }
    }

    private StockEditorRow BuildPreviewRowFromDetail(int qty)
    {
        StockCatalogTaxonomy.ApplyStockModeToFlags(
            DetailStockMode,
            out var trackStock,
            out var orderIn,
            out var notGonna,
            out _,
            out var runOut);
        return new StockEditorRow
        {
            IsActive = DetailIsActive ? 1 : 0,
            StockQty = qty,
            StockMode = DetailStockMode,
            TrackStock = trackStock,
            OrderInMerchandise = orderIn,
            NotGonnaOrderBack = notGonna,
            IsRunOutItem = runOut,
            PreferredStockLevel = ParseOptionalInt(DetailPreferredStockLevelText) ?? ParseOptionalInt(DetailParLevelText),
            WarnMeBelow = ParseOptionalInt(DetailWarnMeBelowText) ?? ParseOptionalInt(LowStockThresholdText),
            LowStockThreshold = ParseOptionalInt(LowStockThresholdText),
            ItemDescription = DetailItemDescription,
        };
    }

    public string BarPriceText
    {
        get => _barPriceText;
        set
        {
            if (SetProperty(ref _barPriceText, value))
            {
                OnPropertyChanged(nameof(BarProfitLine));
                OnPropertyChanged(nameof(PitstopProfitLine));
                OnPropertyChanged(nameof(HeaderVisibilityBadge));
                OnPropertyChanged(nameof(DisplayBarPrice));
            }
        }
    }

    public string PitstopPriceText
    {
        get => _pitstopPriceText;
        set
        {
            if (SetProperty(ref _pitstopPriceText, value))
            {
                OnPropertyChanged(nameof(BarProfitLine));
                OnPropertyChanged(nameof(PitstopProfitLine));
                OnPropertyChanged(nameof(HeaderVisibilityBadge));
                OnPropertyChanged(nameof(DisplayPitstopPrice));
                OnPropertyChanged(nameof(DisplayBarPrice));
            }
        }
    }

    public string CostPriceText
    {
        get => _costPriceText;
        set
        {
            if (SetProperty(ref _costPriceText, value))
            {
                OnPropertyChanged(nameof(BarProfitLine));
                OnPropertyChanged(nameof(PitstopProfitLine));
            }
        }
    }

    public string LowStockThresholdText
    {
        get => _lowStockThresholdText;
        set => SetProperty(ref _lowStockThresholdText, value);
    }

    public bool SpecialEnabled
    {
        get => _specialEnabled;
        set => SetProperty(ref _specialEnabled, value);
    }

    public string SpecialLabel
    {
        get => _specialLabel;
        set => SetProperty(ref _specialLabel, value);
    }

    public string SpecialType
    {
        get => _specialType;
        set => SetProperty(ref _specialType, value);
    }

    public string SpecialValueText
    {
        get => _specialValueText;
        set => SetProperty(ref _specialValueText, value);
    }

    public string SpecialAppliesToMode
    {
        get => _specialAppliesToMode;
        set => SetProperty(ref _specialAppliesToMode, value);
    }

    public string StockAdjustmentText
    {
        get => _stockAdjustmentText;
        set => SetProperty(ref _stockAdjustmentText, value);
    }

    public string StockAdjustmentReason
    {
        get => _stockAdjustmentReason;
        set => SetProperty(ref _stockAdjustmentReason, value);
    }

    /// <summary>Bar shelf profit readout (bar price minus cost).</summary>
    public string BarProfitLine
    {
        get
        {
            if (!StockMoneyInputParser.TryParseMoney(BarPriceText, out var bar) || !StockMoneyInputParser.TryParseMoney(CostPriceText, out var cost))
            {
                return "—";
            }

            return $"Bar profit: {(bar - cost):0.00}";
        }
    }

    /// <summary>Pitstop profit readout (pitstop price minus cost).</summary>
    public string PitstopProfitLine
    {
        get
        {
            if (!StockMoneyInputParser.TryParseMoney(PitstopPriceText, out var pit) || !StockMoneyInputParser.TryParseMoney(CostPriceText, out var cost))
            {
                return "—";
            }

            return $"Pitstop profit: {(pit - cost):0.00}";
        }
    }

    public ObservableCollection<string> SpecialApplyToItems { get; } = new();

    public ObservableCollection<SpecialTypeListItem> SpecialTypeOptionItems { get; } = new();

    public string BarcodeStatusText
    {
        get
        {
            if (!HasSelection)
            {
                return string.Empty;
            }

            var sku = (DetailSku ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(sku))
            {
                return string.Empty;
            }

            var dup = _all.Count(r =>
                r.ItemId != SelectedItemId
                && string.Equals((r.Sku ?? string.Empty).Trim(), sku, StringComparison.OrdinalIgnoreCase));
            if (dup > 0)
            {
                return "Duplicate SKU";
            }

            var altDup = _all.Count(r =>
                r.ItemId != SelectedItemId
                && StockAlternateSkuHelper.AlternateSkuListContains(r.AlternateSkusJson, sku));
            return altDup > 0 ? "Duplicate scan code (alternate)" : string.Empty;
        }
    }

    public bool HasBarcodeWarning => !string.IsNullOrEmpty(BarcodeStatusText);

    public StockListRowViewModel? SelectedPageRow
    {
        get => _selectedPageRow;
        set
        {
            if (!SetProperty(ref _selectedPageRow, value))
            {
                return;
            }

            if (value is null)
            {
                ClearDetail();
                UpdateRowSelectionState();
                return;
            }

            ApplySelectionFromRow(value);
            UpdateRowSelectionState();
        }
    }

    public string SelectedItemHeader =>
        SelectedItemId is > 0 ? (string.IsNullOrWhiteSpace(DetailName) ? "Item" : DetailName.Trim()) : "Select an item";

    public bool ShowSelectedItemHeader => SelectedItemId is > 0;

    /// <summary>When true, item is order-in merchandise (not kept on hand); POS sales do not decrement stock.</summary>
    public bool DetailOrderInMerchandise
    {
        get => _detailOrderInMerchandise;
        set
        {
            if (SetProperty(ref _detailOrderInMerchandise, value))
            {
                OnPropertyChanged(nameof(ShowOrderInMerchandiseDetail));
                OnPropertyChanged(nameof(ShowStockQuantityControls));
            }
        }
    }

    public bool ShowOrderInMerchandiseDetail => HasSelection && DetailOrderInMerchandise;

    /// <summary>On-hand quantity and adjust controls apply only to tracked, on-shelf items.</summary>
    public bool ShowStockQuantityControls =>
        HasSelection && StockCatalogTaxonomy.MaintainsOnHandQuantity(DetailTrackStock ? 1 : 0, DetailOrderInMerchandise ? 1 : 0);

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    /// <summary>Lets code-behind (wizard modals) surface validation errors in the page status row.</summary>
    public void SetStatusMessage(string? message) => StatusMessage = message ?? string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NotifyAllSaveCommandsCanExecuteChanged();
                OnPropertyChanged(nameof(CanInteractWithSelection));
            }
        }
    }

    public long? SelectedItemId
    {
        get => _selectedItemId;
        private set => SetProperty(ref _selectedItemId, value);
    }

    public string DetailName
    {
        get => _detailName;
        set
        {
            if (SetProperty(ref _detailName, value))
            {
                OnPropertyChanged(nameof(SelectedItemHeader));
                OnPropertyChanged(nameof(ShowSelectedItemHeader));
                NotifyAllSaveCommandsCanExecuteChanged();
            }
        }
    }

    public string DetailSku
    {
        get => _detailSku;
        set
        {
            if (SetProperty(ref _detailSku, value))
            {
                OnPropertyChanged(nameof(BarcodeStatusText));
                OnPropertyChanged(nameof(HasBarcodeWarning));
            }
        }
    }

    public string DetailImagePath
    {
        get => _detailImagePath;
        set
        {
            if (SetProperty(ref _detailImagePath, value))
            {
                OnPropertyChanged(nameof(DetailImagePreviewPath));
                OnPropertyChanged(nameof(DetailImagePreviewUri));
                OnPropertyChanged(nameof(HasDetailImagePreview));
                OnPropertyChanged(nameof(DetailImagePreviewEmoji));
                OnPropertyChanged(nameof(HeaderVisibilityBadge));
                RefreshThumbnailForSelectedItem();
            }
        }
    }

    public string? DetailImagePreviewPath =>
        StockItemImageResolver.TryResolve(
            DetailImagePath,
            SelectedRowRawJson,
            DetailCatalogSubCategory);

    public Uri? DetailImagePreviewUri
    {
        get
        {
            var p = DetailImagePreviewPath;
            if (string.IsNullOrEmpty(p))
            {
                return null;
            }

            try
            {
                return new Uri(p, UriKind.Absolute);
            }
            catch (UriFormatException)
            {
                return null;
            }
        }
    }

    public bool HasDetailImagePreview => DetailImagePreviewPath is not null;

    public string DetailImagePreviewEmoji =>
        StockItemImageResolver.GetDisplayEmoji(DetailImagePath, DetailCatalogSubCategory);

    private string? SelectedRowRawJson =>
        SelectedItemId is long id
            ? _all.FirstOrDefault(r => r.ItemId == id)?.RawJson
            : null;

    /// <summary>Bar / Pitstop / Both from current price fields and open-price mode (not the ShowInBar toggle alone).</summary>
    public string HeaderVisibilityBadge
    {
        get
        {
            if (DetailUsesOpenPrice)
            {
                var p = StockMoneyInputParser.TryParseMoney(PitstopPriceText, out var px) && px > 0m;
                return p ? "Open (Bar) + Pitstop" : "Open (Bar)";
            }

            var barPositive = StockMoneyInputParser.TryParseMoney(BarPriceText, out var bx) && bx > 0m;
            var barZero = StockMoneyInputParser.TryParseMoney(BarPriceText, out var b0) && b0 == 0m;
            var pit = StockMoneyInputParser.TryParseMoney(PitstopPriceText, out var px2) && px2 > 0m;
            if (barPositive && pit)
            {
                return "Both";
            }

            if (barPositive)
            {
                return "Bar";
            }

            if (barZero && pit)
            {
                return "Free at bar + Pitstop";
            }

            if (barZero)
            {
                return "Free at bar";
            }

            if (pit)
            {
                return "Pitstop";
            }

            return "—";
        }
    }

    public string DetailStockText
    {
        get => _detailStockText;
        set => SetProperty(ref _detailStockText, value);
    }

    public bool DetailTrackStock
    {
        get => _detailTrackStock;
        set
        {
            if (SetProperty(ref _detailTrackStock, value))
            {
                OnPropertyChanged(nameof(ShowStockQuantityControls));
            }
        }
    }

    public bool DetailIsActive
    {
        get => _detailIsActive;
        set => SetProperty(ref _detailIsActive, value);
    }

    public string DetailCatalogBucket
    {
        get => _detailCatalogBucket;
        set => SetProperty(ref _detailCatalogBucket, value);
    }

    public string DetailCatalogSubCategory
    {
        get => _detailCatalogSubCategory;
        set => SetProperty(ref _detailCatalogSubCategory, value);
    }

    public string DetailStockMode
    {
        get => _detailStockMode;
        set => SetProperty(ref _detailStockMode, value);
    }

    /// <summary>Derive TrackStock / order-in flags from <see cref="DetailStockMode"/> for preview UI.</summary>
    public void SyncDetailFlagsFromStockMode()
    {
        StockCatalogTaxonomy.ApplyStockModeToFlags(
            DetailStockMode,
            out var trackStock,
            out var orderIn,
            out _,
            out _,
            out _);
        DetailTrackStock = trackStock != 0;
        DetailOrderInMerchandise = orderIn != 0;
        OnPropertyChanged(nameof(StockStatusPreview));
    }

    public string DetailGuestPriceText
    {
        get => _detailGuestPriceText;
        set => SetProperty(ref _detailGuestPriceText, value);
    }

    public string DetailBarSpecialText
    {
        get => _detailBarSpecialText;
        set => SetProperty(ref _detailBarSpecialText, value);
    }

    public string DetailGuestSpecialText
    {
        get => _detailGuestSpecialText;
        set => SetProperty(ref _detailGuestSpecialText, value);
    }

    public string DetailPitstopSpecialText
    {
        get => _detailPitstopSpecialText;
        set => SetProperty(ref _detailPitstopSpecialText, value);
    }

    public string DetailAlternateSkusText
    {
        get => _detailAlternateSkusText;
        set => SetProperty(ref _detailAlternateSkusText, value);
    }

    public string DetailItemDescription
    {
        get => _detailItemDescription;
        set => SetProperty(ref _detailItemDescription, value);
    }

    public string DetailItemType
    {
        get => _detailItemType;
        private set
        {
            if (SetProperty(ref _detailItemType, value))
            {
                OnPropertyChanged(nameof(IsShotMixerDetail));
            }
        }
    }

    public string DetailShotMixerSpiritsText
    {
        get => _detailShotMixerSpiritsText;
        set => SetProperty(ref _detailShotMixerSpiritsText, value);
    }

    public bool IsShotMixerDetail => ShotMixerCatalog.IsShotMixerItem(DetailName, DetailItemType);

    public bool DetailIsOnSpecial
    {
        get => _detailIsOnSpecial;
        set => SetProperty(ref _detailIsOnSpecial, value);
    }

    public string DetailCatalogDisplay
    {
        get => _detailCatalogDisplay;
        private set => SetProperty(ref _detailCatalogDisplay, value);
    }

    /// <summary>When true, item may appear on the bar POS catalog (still requires bar price or open price).</summary>
    public bool ShowInBar
    {
        get => _showInBar;
        set => SetProperty(ref _showInBar, value);
    }

    /// <summary>When true, item is eligible for the Pitstop retail grid (still requires a Pitstop price row).</summary>
    public bool ShowInPitstop
    {
        get => _showInPitstop;
        set => SetProperty(ref _showInPitstop, value);
    }

    public bool HasSelection => SelectedItemId is > 0;

    public bool CanInteractWithSelection => HasSelection && !IsBusy;

    public string DisplayBarPrice =>
        DetailUsesOpenPrice
            ? (string.IsNullOrWhiteSpace(BarPriceText) ? "Open price" : $"Open (hint {BarPriceText.Trim()})")
            : (StockMoneyInputParser.TryParseMoney(BarPriceText, out var barDec)
                ? barDec == 0m
                    ? "Free ($0.00)"
                    : BarPriceText.Trim()
                : "—");

    public string DisplayPitstopPrice => string.IsNullOrWhiteSpace(PitstopPriceText) ? "—" : PitstopPriceText.Trim();

    /// <summary>When true, Add Drinks prompts for unit price on each add (Items.UsesOpenPrice).</summary>
    public bool DetailUsesOpenPrice
    {
        get => _detailUsesOpenPrice;
        set
        {
            if (SetProperty(ref _detailUsesOpenPrice, value))
            {
                OnPropertyChanged(nameof(DisplayBarPrice));
                OnPropertyChanged(nameof(HeaderVisibilityBadge));
            }
        }
    }

    public IRelayCommand<int> SelectFilterCardCommand { get; }

    public IRelayCommand OpenItemEditCommand { get; }

    public IRelayCommand NavigateHomeCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand SaveItemSectionCommand { get; }

    public IAsyncRelayCommand SavePricesCommand { get; }

    public IAsyncRelayCommand SaveStockSettingsCommand { get; }

    public IAsyncRelayCommand SaveBarcodeCommand { get; }

    public IRelayCommand<StockListRowViewModel?> SelectRowCommand { get; }

    public IRelayCommand<string> SelectBrowserFilterCommand { get; }

    public IRelayCommand<string> StockDeltaCommand { get; }

    public IAsyncRelayCommand<string> SummaryQuickStockCommand { get; }

    public IRelayCommand ApplyStockAdjustmentCommand { get; }

    public IRelayCommand ClearBarcodeCommand { get; }

    public IAsyncRelayCommand ScanBarcodePlaceholderCommand { get; }

    /// <summary>Reloads categories and stock rows from SQLite. Returns false on failure (see <see cref="StatusMessage"/>).</summary>
    public async Task<bool> LoadAsync(bool resetStatusMessage = true)
    {
        if (resetStatusMessage)
        {
            StatusMessage = string.Empty;
        }

        try
        {
            await ReloadCategoriesAsync().ConfigureAwait(true);
            _all = (await _stock.GetStockRowsAsync(true).ConfigureAwait(true)).ToList();
            var lastCount = await _stock.GetLatestStockCountDateAsync().ConfigureAwait(true);
            _lastStockCountDisplay = FormatStockCountDate(lastCount);
            OnPropertyChanged(nameof(LastStockCountDisplay));
            RebindPage();
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>Persists the summary/detail fields for the selected item (including prices and stock) and reloads. Returns false if validation failed or nothing was written.</summary>
    public async Task<bool> TryPersistCurrentItemAsync(string? stockMovementReferenceOverride = null) =>
        await PersistFullItemAsync(stockMovementReferenceOverride).ConfigureAwait(true);

    /// <summary>Sets a built-in stock icon (<c>stock-icon:{id}</c>) and optionally persists.</summary>
    public async Task<bool> ApplyStockProductIconAsync(string iconId, bool persistImmediately = true)
    {
        if (!StockProductIconCatalog.TryGetById(iconId, out _))
        {
            StatusMessage = "Unknown product icon.";
            return false;
        }

        TryDeleteManagedProductImage(DetailImagePath);
        DetailImagePath = StockProductIconCatalog.ToStoragePath(iconId);
        if (persistImmediately && HasSelection)
        {
            return await TryPersistCurrentItemAsync().ConfigureAwait(true);
        }

        PulseCatalogRefresh();
        return true;
    }

    /// <summary>Clears custom photo and built-in icon (category emoji is used until a new choice).</summary>
    public async Task<bool> ClearProductImageAsync(bool persistImmediately = true)
    {
        TryDeleteManagedProductImage(DetailImagePath);
        DetailImagePath = string.Empty;
        if (persistImmediately && HasSelection)
        {
            return await TryPersistCurrentItemAsync().ConfigureAwait(true);
        }

        PulseCatalogRefresh();
        return true;
    }

    /// <summary>Copies a picked image into app storage, updates detail path, and optionally persists.</summary>
    public async Task<bool> ApplyProductImageFromPickerAsync(string sourcePath, bool persistImmediately = true)
    {
        TryDeleteManagedProductImage(DetailImagePath);
        var result = _productImages.ImportFromSource(sourcePath, SelectedItemId);
        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "Could not save image.";
            return false;
        }

        DetailImagePath = result.StoredPath!;
        if (persistImmediately && HasSelection)
        {
            return await TryPersistCurrentItemAsync().ConfigureAwait(true);
        }

        PulseCatalogRefresh();
        return true;
    }

    private void TryDeleteManagedProductImage(string? path)
    {
        if (!_productImages.IsManagedPath(path))
        {
            return;
        }

        try
        {
            File.Delete(path!.Trim());
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public void RefreshThumbnailForSelectedItem()
    {
        // List rows no longer show thumbnails; product image updates apply on next reload.
    }

    public void PulseCatalogRefresh()
    {
        _barCatalogCache.Invalidate();
        _refreshBus.RequestRefresh();
    }

    /// <summary>Permanently removes the selected item from SQLite (prices and stock movements cascade). Clears selection and reloads the list.</summary>
    public async Task DeleteSelectedItemAsync()
    {
        if (SelectedItemId is not long id || IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            await _stock.PermanentlyDeleteStockItemAsync(id).ConfigureAwait(true);
            SelectedPageRow = null;
            var ok = await LoadAsync(resetStatusMessage: false).ConfigureAwait(true);
            StatusMessage = ok ? "Item deleted." : "Item was deleted, but the list could not reload. Tap Refresh.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task ReloadCategoriesAsync()
    {
        CatalogFilterOptions.Clear();
        foreach (var label in StockCatalogTaxonomy.AllFilterCategoryLabels())
        {
            CatalogFilterOptions.Add(label);
        }

        return Task.CompletedTask;
    }

    private void SelectFilterCard(int index) => BrowserFilterIndex = index;

    private void RefreshFilterCardSelection()
    {
        foreach (var card in FilterCards)
        {
            card.SetSelected(card.FilterIndex == BrowserFilterIndex);
        }
    }

    private void RefreshFilterCounts()
    {
        foreach (var card in FilterCards)
        {
            card.Count = StockManagementBrowserFilter.CountForFilter(_all, card.FilterIndex);
        }
    }

    private void OpenItemEditFromSelection()
    {
        if (!HasSelection)
        {
            return;
        }

        CurrentScreen = StockManagementScreen.ItemEdit;
    }

    public void AdjustQuantityDelta(int delta)
    {
        _adjustQuantity = Math.Max(0, _adjustQuantity + delta);
        OnPropertyChanged(nameof(AdjustQuantityText));
    }

    public void ApplyQuickFilter(int filterIndex) => BrowserFilterIndex = filterIndex;

    private void ApplyStockDeltaFromParameter(string? parameter)
    {
        if (!int.TryParse(parameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var delta))
        {
            return;
        }

        ApplyStockDelta(delta);
    }

    private bool CanSummaryQuickStock()
    {
        if (!HasSelection || IsBusy)
        {
            return false;
        }

        var r = _all.FirstOrDefault(x => x.ItemId == SelectedItemId);
        if (r is null)
        {
            return false;
        }

        if (!StockCatalogTaxonomy.MaintainsOnHandQuantity(r.TrackStock, r.OrderInMerchandise))
        {
            return false;
        }

        return !StockCatalogTaxonomy.IsBarLineFood(r.CatalogBucket, r.CatalogSubCategory);
    }

    private async Task ApplyQuickStockDeltaAndPersistAsync(string? parameter)
    {
        if (!HasSelection || IsBusy)
        {
            return;
        }

        if (!int.TryParse(parameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var delta))
        {
            return;
        }

        await ApplyQuickStockDeltaAndPersistCoreAsync(delta).ConfigureAwait(true);
    }

    public async Task<bool> ApplyQuickStockDeltaAndPersistCoreAsync(int delta)
    {
        if (!HasSelection || IsBusy)
        {
            return false;
        }

        ApplyStockDelta(delta);
        return await PersistFullItemAsync(null).ConfigureAwait(true);
    }

    private void NotifyAllSaveCommandsCanExecuteChanged()
    {
        SaveItemSectionCommand.NotifyCanExecuteChanged();
        SavePricesCommand.NotifyCanExecuteChanged();
        SaveStockSettingsCommand.NotifyCanExecuteChanged();
        SaveBarcodeCommand.NotifyCanExecuteChanged();
        SummaryQuickStockCommand.NotifyCanExecuteChanged();
        ScanBarcodePlaceholderCommand.NotifyCanExecuteChanged();
    }

    private void SelectBrowserFilterFromParameter(string? parameter)
    {
        if (int.TryParse(parameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
        {
            BrowserFilterIndex = idx;
        }
    }

    private void UpdateRowSelectionState()
    {
        var sel = SelectedItemId;
        foreach (var r in PageRows)
        {
            r.IsSelected = sel is long id && r.ItemId == id;
        }
    }

    private List<StockEditorRow> FilteredRows() =>
        StockManagementBrowserFilter.Apply(_all, SearchText, BrowserFilterIndex).ToList();

    private void RebindPage()
    {
        RefreshFilterCounts();
        RefreshFilterCardSelection();
        var filtered = FilteredRows();

        PageRows.Clear();
        var index = 0;
        foreach (var row in filtered)
        {
            PageRows.Add(new StockListRowViewModel(row, index % 2 == 1));
            index++;
        }

        if (SelectedItemId is long id && filtered.All(r => r.ItemId != id))
        {
            ClearDetail();
        }
        else
        {
            SyncListSelectionToPage();
        }

        UpdateRowSelectionState();
        OnPropertyChanged(nameof(ItemCountText));
        OnPropertyChanged(nameof(NeedBuyingCount));
        OnPropertyChanged(nameof(NeedBuyingBadgeText));
        OnPropertyChanged(nameof(ShowNeedBuyingBadge));
    }

    public IReadOnlyList<StockShoppingListRowViewModel> BuildShoppingListRows() =>
        BuildShoppingListRows(includeMerch: true, includeRegular: true);

    public IReadOnlyList<StockShoppingListRowViewModel> BuildShoppingListRegularRows() =>
        BuildShoppingListRows(includeMerch: false, includeRegular: true);

    public IReadOnlyList<StockShoppingListRowViewModel> BuildShoppingListMerchRows() =>
        BuildShoppingListRows(includeMerch: true, includeRegular: false);

    private IReadOnlyList<StockShoppingListRowViewModel> BuildShoppingListRows(bool includeMerch, bool includeRegular) =>
        _all.Where(StockInventoryLevelHelper.IsShoppingListCandidate)
            .Select(r => new StockShoppingListRowViewModel(r))
            .Where(r => (r.IsMerch && includeMerch) || (!r.IsMerch && includeRegular))
            .OrderBy(r => r.SortPriority)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public string BuildShoppingListClipboardText()
    {
        var regular = BuildShoppingListRegularRows();
        var merch = BuildShoppingListMerchRows();
        var lines = new List<string>
        {
            "Nickeltown Shopping List",
            DateTime.Now.ToString("dddd d MMMM yyyy", CultureInfo.GetCultureInfo("en-AU")),
            string.Empty,
        };

        if (regular.Count == 0 && merch.Count == 0)
        {
            lines.Add("Nothing needs buying right now.");
            return string.Join(Environment.NewLine, lines);
        }

        var totalItems = regular.Count + merch.Count;
        var totalNeed = regular.Sum(r => r.NeedQty) + merch.Sum(r => r.NeedQty);
        lines.Add($"Items: {totalItems}  |  Units needed: {totalNeed}");
        if (regular.Count > 0 && merch.Count > 0)
        {
            lines.Add($"Bar & supplies: {regular.Count}  |  Merchandise: {merch.Count}");
        }

        lines.Add(string.Empty);
        AppendShoppingListSection(lines, "BAR & SUPPLIES", regular);
        AppendShoppingListSection(lines, "MERCHANDISE", merch);
        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendShoppingListSection(List<string> lines, string title, IReadOnlyList<StockShoppingListRowViewModel> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        lines.Add(title);
        lines.Add(new string('─', Math.Min(title.Length, 40)));
        foreach (var row in rows)
        {
            lines.Add(row.Name + (string.IsNullOrEmpty(row.StatusText) ? "" : "  [" + row.StatusText + "]"));
            lines.Add($"  Have: {row.HaveQty}   Need: {row.NeedQty}");
            if (!string.IsNullOrEmpty(row.SuggestedDisplayLine))
            {
                lines.Add("  " + row.SuggestedDisplayLine);
            }

            if (!string.IsNullOrEmpty(row.SetupHint))
            {
                lines.Add("  " + row.SetupHint);
            }

            lines.Add(string.Empty);
        }
    }

    public IReadOnlyList<StockEditorRow> BuildStockCountQueue() =>
        _all.Where(StockInventoryLevelHelper.IsTrackedStockItem)
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public StockEditorRow? FindItemBySearch(string needle)
    {
        var matches = FindItemsBySearch(needle, maxResults: 1);
        return matches.Count > 0 ? matches[0] : null;
    }

    public IReadOnlyList<StockEditorRow> FindItemsBySearch(string needle, int maxResults = 8)
    {
        var n = (needle ?? string.Empty).Trim();
        if (n.Length == 0 || maxResults <= 0)
        {
            return Array.Empty<StockEditorRow>();
        }

        return _all
            .Where(r =>
                (r.Name ?? string.Empty).Contains(n, StringComparison.OrdinalIgnoreCase)
                || (r.Sku ?? string.Empty).Contains(n, StringComparison.OrdinalIgnoreCase)
                || StockAlternateSkuHelper.AlternateSkuListContains(r.AlternateSkusJson, n))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToList();
    }

    private static string FormatStockCountDate(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso))
        {
            return "—";
        }

        if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
        {
            return dt.ToLocalTime().ToString("d MMM yyyy", CultureInfo.GetCultureInfo("en-AU"));
        }

        return iso.Trim();
    }

    private void SyncListSelectionToPage()
    {
        StockListRowViewModel? match = null;
        if (SelectedItemId is long selId)
        {
            match = PageRows.FirstOrDefault(r => r.ItemId == selId);
        }

        if (!ReferenceEquals(_selectedPageRow, match))
        {
            _selectedPageRow = match;
            OnPropertyChanged(nameof(SelectedPageRow));
        }
    }

    private void ApplySelectionFromRow(StockListRowViewModel row)
    {
        var src = _all.FirstOrDefault(r => r.ItemId == row.ItemId);
        if (src is null)
        {
            return;
        }

        var snap = StockItemDetailSnapshot.FromRow(src);
        SelectedItemId = snap.ItemId;
        DetailName = snap.Name;
        DetailItemType = snap.ItemType;
        DetailShotMixerSpiritsText = snap.ShotMixerSpiritsText;
        DetailSku = snap.Sku;
        DetailImagePath = snap.ImagePath;
        DetailStockText = snap.StockText;
        DetailTrackStock = snap.TrackStock;
        DetailIsActive = snap.IsActive;
        DetailCatalogBucket = snap.CatalogBucket;
        DetailCatalogSubCategory = snap.CatalogSubCategory;
        DetailStockMode = snap.StockMode;
        DetailCatalogDisplay = snap.CatalogDisplay;
        BarPriceText = snap.BarPriceText;
        PitstopPriceText = snap.PitstopPriceText;
        DetailGuestPriceText = snap.GuestPriceText;
        DetailBarSpecialText = snap.BarSpecialText;
        DetailGuestSpecialText = snap.GuestSpecialText;
        DetailPitstopSpecialText = snap.PitstopSpecialText;
        DetailIsOnSpecial = snap.IsOnSpecial;
        DetailAlternateSkusText = snap.AlternateSkusText;
        DetailItemDescription = snap.ItemDescription;
        CostPriceText = snap.CostPriceText;
        LowStockThresholdText = snap.LowStockThresholdText;
        DetailUsesOpenPrice = snap.UsesOpenPrice;
        ShowInBar = snap.ShowInBar;
        ShowInPitstop = snap.ShowInPitstop;
        DetailOrderInMerchandise = snap.OrderInMerchandise;
        StockAdjustmentText = string.Empty;
        StockAdjustmentReason = string.Empty;
        SpecialEnabled = DetailIsOnSpecial;
        SpecialType = snap.SpecialType;
        SpecialValueText = snap.SpecialValueText;
        SpecialLabel = snap.SpecialLabel;
        SpecialAppliesToMode = snap.SpecialAppliesToMode;
        ApplyMetadataFromDescription(snap.ItemDescription, snap.IsShotMixerItem);
        DetailPreferredStockLevelText = src.PreferredStockLevel?.ToString(CultureInfo.InvariantCulture)
            ?? DetailParLevelText;
        DetailWarnMeBelowText = src.WarnMeBelow?.ToString(CultureInfo.InvariantCulture)
            ?? snap.LowStockThresholdText;
        DetailPurchaseUnitQtyText = src.PurchaseUnitQty?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        DetailShowOnShoppingList = src.ShowOnShoppingList is null || src.ShowOnShoppingList != 0;
        OnPropertyChanged(nameof(StockStatusPreview));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanInteractWithSelection));
        OnPropertyChanged(nameof(ShowStockQuantityControls));
        OnPropertyChanged(nameof(SelectedItemHeader));
        OnPropertyChanged(nameof(ShowSelectedItemHeader));
        OnPropertyChanged(nameof(BarcodeStatusText));
        OnPropertyChanged(nameof(HasBarcodeWarning));
        OnPropertyChanged(nameof(BarProfitLine));
        OnPropertyChanged(nameof(PitstopProfitLine));
        OnPropertyChanged(nameof(HeaderVisibilityBadge));
        NotifyAllSaveCommandsCanExecuteChanged();
        StockDeltaCommand.NotifyCanExecuteChanged();
        ApplyStockAdjustmentCommand.NotifyCanExecuteChanged();
        ClearBarcodeCommand.NotifyCanExecuteChanged();
        ScanBarcodePlaceholderCommand.NotifyCanExecuteChanged();
        SummaryQuickStockCommand.NotifyCanExecuteChanged();
        OpenItemEditCommand.NotifyCanExecuteChanged();
    }

    private void ApplyMetadataFromDescription(string? itemDescription, bool isShotMixer)
    {
        var meta = StockItemMetadataSerializer.Parse(itemDescription, isShotMixer);
        DetailParLevelText = meta.ParLevel?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        DetailNotesText = meta.Notes;
        DetailMixerItemId = meta.MixerItemId;
        DetailMixerQty = meta.MixerQty;
        if (isShotMixer && meta.Spirits is { Count: > 0 })
        {
            DetailShotMixerSpiritsText = string.Join(Environment.NewLine, meta.Spirits);
        }
    }

    private void ClearDetail()
    {
        SelectedItemId = null;
        DetailName = string.Empty;
        DetailItemType = "Item";
        DetailShotMixerSpiritsText = string.Empty;
        DetailSku = string.Empty;
        DetailImagePath = string.Empty;
        DetailStockText = "0";
        DetailTrackStock = true;
        DetailIsActive = true;
        DetailCatalogBucket = StockCatalogTaxonomy.BucketBar;
        DetailCatalogSubCategory = "Drinks";
        DetailStockMode = StockCatalogTaxonomy.StockModeTracked;
        DetailCatalogDisplay = string.Empty;
        DetailGuestPriceText = string.Empty;
        DetailBarSpecialText = string.Empty;
        DetailGuestSpecialText = string.Empty;
        DetailPitstopSpecialText = string.Empty;
        DetailAlternateSkusText = string.Empty;
        DetailItemDescription = string.Empty;
        DetailIsOnSpecial = false;
        BarPriceText = string.Empty;
        PitstopPriceText = string.Empty;
        CostPriceText = string.Empty;
        StockAdjustmentText = string.Empty;
        StockAdjustmentReason = string.Empty;
        LowStockThresholdText = string.Empty;
        DetailUsesOpenPrice = false;
        ShowInBar = false;
        ShowInPitstop = false;
        DetailOrderInMerchandise = false;
        SpecialEnabled = false;
        SpecialValueText = string.Empty;
        SpecialAppliesToMode = "Bar";
        SpecialLabel = string.Empty;
        SpecialType = "FixedPrice";
        DetailParLevelText = string.Empty;
        DetailPreferredStockLevelText = string.Empty;
        DetailWarnMeBelowText = string.Empty;
        DetailPurchaseUnitQtyText = string.Empty;
        DetailShowOnShoppingList = true;
        DetailNotesText = string.Empty;
        DetailMixerItemId = null;
        DetailMixerQty = 1;
        OnPropertyChanged(nameof(StockStatusPreview));
        if (_selectedPageRow is not null)
        {
            _selectedPageRow = null;
            OnPropertyChanged(nameof(SelectedPageRow));
        }

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowOrderInMerchandiseDetail));
        OnPropertyChanged(nameof(ShowStockQuantityControls));
        OnPropertyChanged(nameof(CanInteractWithSelection));
        OnPropertyChanged(nameof(SelectedItemHeader));
        OnPropertyChanged(nameof(ShowSelectedItemHeader));
        OnPropertyChanged(nameof(BarcodeStatusText));
        OnPropertyChanged(nameof(HasBarcodeWarning));
        OnPropertyChanged(nameof(BarProfitLine));
        OnPropertyChanged(nameof(PitstopProfitLine));
        OnPropertyChanged(nameof(HeaderVisibilityBadge));
        NotifyAllSaveCommandsCanExecuteChanged();
        StockDeltaCommand.NotifyCanExecuteChanged();
        ApplyStockAdjustmentCommand.NotifyCanExecuteChanged();
        ClearBarcodeCommand.NotifyCanExecuteChanged();
        ScanBarcodePlaceholderCommand.NotifyCanExecuteChanged();
        SummaryQuickStockCommand.NotifyCanExecuteChanged();
    }

    private bool CanSaveDetail() =>
        !IsBusy && SelectedItemId is > 0 && !string.IsNullOrWhiteSpace(DetailName);

    /// <summary>Creates a blank item row and loads it into the detail editor (full-screen add flow).</summary>
    public async Task<bool> BeginNewItemDraftAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var id = await _stock.CreateStockItemAsync().ConfigureAwait(true);
            _newItemDraftId = id;
            ClearDetail();
            SelectedItemId = id;
            DetailCatalogBucket = StockCatalogTaxonomy.BucketBar;
            DetailCatalogSubCategory = StockCatalogTaxonomy.DefaultSubCategory(StockCatalogTaxonomy.BucketBar);
            DetailStockMode = StockCatalogTaxonomy.StockModeTracked;
            var (showBar, showPit) = StockCatalogTaxonomy.ExpectedVisibilityForBucket(DetailCatalogBucket);
            ShowInBar = showBar != 0;
            ShowInPitstop = showPit != 0;
            DetailIsActive = true;
            DetailTrackStock = true;
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(CanInteractWithSelection));
            NotifyAllSaveCommandsCanExecuteChanged();
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _newItemDraftId = null;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Removes an abandoned add-item draft when the user backs out without saving a name.</summary>
    public async Task AbandonNewItemDraftIfNeededAsync()
    {
        if (_newItemDraftId is not long id)
        {
            return;
        }

        _newItemDraftId = null;
        if (string.IsNullOrWhiteSpace(DetailName))
        {
            try
            {
                await _stock.PermanentlyDeleteStockItemAsync(id).ConfigureAwait(true);
                if (SelectedItemId == id)
                {
                    ClearDetail();
                }

                await LoadAsync(resetStatusMessage: false).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
        }
    }

    public void ClearNewItemDraftFlag() => _newItemDraftId = null;

    private void ApplyStockDelta(int delta)
    {
        if (!HasSelection)
        {
            return;
        }

        if (!int.TryParse(DetailStockText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty) || qty < 0)
        {
            qty = 0;
        }

        qty = Math.Max(0, qty + delta);
        DetailStockText = qty.ToString(CultureInfo.InvariantCulture);
    }

    private void ApplyStockAdjustmentFromText()
    {
        if (!HasSelection)
        {
            return;
        }

        var raw = (StockAdjustmentText ?? string.Empty).Trim();
        if (!decimal.TryParse(
                raw,
                NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var dAdj))
        {
            StatusMessage = "Enter a numeric adjustment (whole units, e.g. -6 or 12).";
            return;
        }

        if (dAdj != decimal.Truncate(dAdj))
        {
            StatusMessage = "Stock adjustment must be a whole number (no decimals).";
            return;
        }

        var delta = (int)dAdj;
        if (delta == 0)
        {
            StatusMessage = "Enter a non-zero adjustment.";
            return;
        }

        ApplyStockDelta(delta);
        StockAdjustmentText = string.Empty;
        StatusMessage = string.Empty;
    }

    private void ClearBarcode() => DetailSku = string.Empty;

    private async Task ScanBarcodeAsync()
    {
        if (!HasSelection || IsBusy)
        {
            return;
        }

        var draft = (DetailSku ?? string.Empty).Trim();
        var entered = await _input.ShowKeyboardAsync(draft, "Scan or type barcode / SKU", CancellationToken.None).ConfigureAwait(true);
        if (entered is null)
        {
            return;
        }

        DetailSku = entered.Trim();
        StatusMessage = string.IsNullOrWhiteSpace(DetailSku) ? string.Empty : "SKU updated — tap Save barcode to persist.";
    }

    private async Task<bool> PersistFullItemAsync(string? stockMovementReferenceOverride)
    {
        if (!CanSaveDetail() || SelectedItemId is not long id)
        {
            if (SelectedItemId is not > 0)
            {
                StatusMessage = "Cannot save: select an item first.";
            }
            else if (string.IsNullOrWhiteSpace(DetailName))
            {
                StatusMessage = "Cannot save: item name is required.";
            }
            else if (IsBusy)
            {
                StatusMessage = "Please wait for the current operation to finish.";
            }
            else
            {
                StatusMessage = "Cannot save right now.";
            }

            return false;
        }

        if (!StockMoneyInputParser.TryParseWholeNonNegativeInt(DetailStockText, "Stock quantity", out var qty, out var qtyErr))
        {
            StatusMessage = qtyErr;
            return false;
        }

        if (DetailIsOnSpecial)
        {
            if (IsShotMixerDetail)
            {
                if (!StockMoneyInputParser.TryParseMoney(DetailBarSpecialText, out var memberSpecial)
                    || memberSpecial < 0m)
                {
                    StatusMessage = string.IsNullOrWhiteSpace(DetailBarSpecialText)
                        ? "Enter a member tab special price or turn off promotional special pricing."
                        : "Enter a valid member tab special price.";
                    return false;
                }
            }
            else
            {
                var hasSpecialPrice = StockMoneyInputParser.TryParseMoney(DetailBarSpecialText, out _)
                    || StockMoneyInputParser.TryParseMoney(DetailGuestSpecialText, out _)
                    || StockMoneyInputParser.TryParseMoney(DetailPitstopSpecialText, out _);
                if (!hasSpecialPrice)
                {
                    StatusMessage = "Enter at least one special price or turn off promotional special pricing.";
                    return false;
                }
            }
        }

        var warnRaw = (DetailWarnMeBelowText ?? string.Empty).Trim();
        int? warnVal = null;
        if (!string.IsNullOrEmpty(warnRaw))
        {
            if (!StockMoneyInputParser.TryParseWholeNonNegativeInt(warnRaw, "Warn Me Below", out var wv, out var warnErr))
            {
                StatusMessage = warnErr;
                return false;
            }

            warnVal = wv;
        }

        var lowRaw = (LowStockThresholdText ?? string.Empty).Trim();
        int? lowVal = warnVal;
        if (!string.IsNullOrEmpty(lowRaw))
        {
            if (!StockMoneyInputParser.TryParseWholeNonNegativeInt(lowRaw, "Warn Me Below", out var lt, out var lowErr))
            {
                StatusMessage = lowErr;
                return false;
            }

            lowVal = lt;
            warnVal ??= lt;
        }

        int? preferredVal = null;
        var prefRaw = (DetailPreferredStockLevelText ?? DetailParLevelText ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(prefRaw))
        {
            if (!StockMoneyInputParser.TryParseWholeNonNegativeInt(prefRaw, "Target Amount", out var pv, out var prefErr))
            {
                StatusMessage = prefErr;
                return false;
            }

            preferredVal = pv;
        }

        int? packVal = null;
        var packRaw = (DetailPurchaseUnitQtyText ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(packRaw))
        {
            if (!StockMoneyInputParser.TryParseWholeNonNegativeInt(packRaw, "Purchase unit", out var pk, out var packErr))
            {
                StatusMessage = packErr;
                return false;
            }

            packVal = pk;
        }

        double? costVal = StockMoneyInputParser.TryParseMoney(CostPriceText, out var costDec) ? (double)costDec : null;

        IsBusy = true;
        StatusMessage = string.Empty;
        var ok = false;
        try
        {
            var movementRef = stockMovementReferenceOverride
                ?? (string.IsNullOrWhiteSpace(StockAdjustmentReason) ? null : StockAdjustmentReason.Trim());
            var bucket = StockCatalogTaxonomy.NormalizeBucket(DetailCatalogBucket);
            var sub = StockCatalogTaxonomy.NormalizeSubCategory(bucket, DetailCatalogSubCategory);
            var stockMode = StockCatalogTaxonomy.NormalizeStockMode(DetailStockMode);
            var showInBar = ShowInBar;
            var saveQty = qty;
            string? itemDescription;
            if (IsShotMixerDetail)
            {
                bucket = StockCatalogTaxonomy.BucketBar;
                sub = "Spirits";
                stockMode = StockCatalogTaxonomy.StockModeNotTracked;
                showInBar = false;
                saveQty = 0;
                var spirits = (DetailShotMixerSpiritsText ?? string.Empty).Split(
                    ['\r', '\n', ','],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                itemDescription = StockItemMetadataSerializer.ToStorageJson(
                    new StockItemMetadataSerializer.StockItemMetadata
                    {
                        Notes = DetailNotesText,
                        ParLevel = preferredVal ?? ParseOptionalInt(DetailParLevelText),
                        MixerItemId = DetailMixerItemId,
                        MixerQty = DetailMixerQty,
                        Spirits = spirits.ToList(),
                    },
                    includeSpirits: true);
            }
            else
            {
                itemDescription = StockItemMetadataSerializer.ToStorageJson(
                    new StockItemMetadataSerializer.StockItemMetadata
                    {
                        Notes = DetailNotesText,
                        ParLevel = preferredVal ?? ParseOptionalInt(DetailParLevelText),
                    },
                    includeSpirits: false);
                if (string.IsNullOrEmpty(itemDescription) && !string.IsNullOrWhiteSpace(DetailNotesText))
                {
                    itemDescription = DetailNotesText.Trim();
                }
            }

            StockCatalogTaxonomy.ApplyStockModeToFlags(stockMode, out var tr, out var ordIn, out var ngb, out var incWeekly, out var runOut);
            var altJson = StockAlternateSkuHelper.SerializeAlternateSkusFromMultiline(DetailAlternateSkusText);
            var imagePath = string.IsNullOrWhiteSpace(DetailImagePath)
                ? null
                : _productImages.EnsureItemIdFileName(DetailImagePath.Trim(), id) ?? DetailImagePath.Trim();
            if (!string.Equals(imagePath, DetailImagePath, StringComparison.Ordinal))
            {
                DetailImagePath = imagePath ?? string.Empty;
            }

            var write = new StockItemAdminWrite
            {
                ItemId = id,
                Name = DetailName.Trim(),
                Sku = string.IsNullOrWhiteSpace(DetailSku) ? null : DetailSku.Trim(),
                StockQty = saveQty,
                TrackStock = tr,
                ImagePath = imagePath,
                IsActive = DetailIsActive ? 1 : 0,
                StockMovementReference = movementRef,
                CostPrice = costVal,
                LowStockThreshold = lowVal,
                PreferredStockLevel = preferredVal,
                WarnMeBelow = warnVal,
                PurchaseUnitQty = packVal,
                ShowOnShoppingList = DetailShowOnShoppingList ? 1 : 0,
                UsesOpenPrice = DetailUsesOpenPrice ? 1 : 0,
                ShowInPitstop = ShowInPitstop ? 1 : 0,
                ShowInBar = showInBar ? 1 : 0,
                OrderInMerchandise = ordIn,
                CatalogBucket = bucket,
                CatalogSubCategory = sub,
                StockMode = stockMode,
                NotGonnaOrderBack = ngb,
                IncludeInWeeklyStockReport = incWeekly,
                IsRunOutItem = runOut,
                IsSharedItem = bucket == StockCatalogTaxonomy.BucketShared ? 1 : 0,
                IsOnSpecial = DetailIsOnSpecial ? 1 : 0,
                SpecialType = DetailIsOnSpecial ? SpecialType : null,
                SpecialValue = DetailIsOnSpecial ? SpecialValueText : null,
                SpecialLabel = DetailIsOnSpecial ? SpecialLabel : null,
                SpecialAppliesTo = DetailIsOnSpecial ? SpecialAppliesToMode : null,
                AlternateSkusJson = altJson,
                ItemDescription = itemDescription,
                CategoryId = null,
            };
            await _persist.UpdateItemAdminAsync(write, CancellationToken.None).ConfigureAwait(true);

            await _persist.UpsertPriceIfParsedAsync(id, "Bar", BarPriceText, CancellationToken.None).ConfigureAwait(true);
            await _persist.UpsertPriceIfParsedAsync(id, "Pitstop", PitstopPriceText, CancellationToken.None).ConfigureAwait(true);
            await _persist.UpsertPriceIfParsedAsync(id, "Guest", DetailGuestPriceText, CancellationToken.None).ConfigureAwait(true);
            await _persist
                .ApplySpecialPricesAsync(
                    id,
                    DetailIsOnSpecial,
                    DetailBarSpecialText,
                    DetailGuestSpecialText,
                    DetailPitstopSpecialText,
                    CancellationToken.None)
                .ConfigureAwait(true);

            var reloadOk = await LoadAsync(resetStatusMessage: false).ConfigureAwait(true);
            if (reloadOk)
            {
                var restored = _all.FirstOrDefault(r => r.ItemId == id);
                if (restored is not null)
                {
                    ApplySelectionFromRow(new StockListRowViewModel(restored));
                    SyncListSelectionToPage();
                    UpdateRowSelectionState();
                }

                StatusMessage = "Saved.";
            }
            else
            {
                StatusMessage = "Changes were saved, but the product list could not reload. Tap Refresh.";
            }

            ok = true;
            ClearNewItemDraftFlag();
            PulseCatalogRefresh();
            if (IsShotMixerDetail)
            {
                _shotMixerConfig.Invalidate();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            NotifyAllSaveCommandsCanExecuteChanged();
        }

        return ok;
    }

    public async Task CreateItemFromWizardAsync(
        string name,
        string catalogBucket,
        string catalogSubCategory,
        string stockMode,
        string? sku,
        string? barPriceText,
        string? pitstopPriceText,
        int startingStock,
        string? lowStockThresholdText,
        bool showInBar,
        bool showInPitstop,
        string? imagePathOptional,
        bool usesOpenPriceAtBar = false)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            StatusMessage = "Name is required.";
            return;
        }

        if (startingStock < 0)
        {
            StatusMessage = "Starting stock must be non-negative.";
            return;
        }

        int? lowVal = null;
        var lowRaw = (lowStockThresholdText ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(lowRaw))
        {
            if (!StockMoneyInputParser.TryParseWholeNonNegativeInt(lowRaw, "Low stock threshold", out var lt, out var lowErr))
            {
                StatusMessage = lowErr;
                return;
            }

            lowVal = lt;
        }

        StatusMessage = string.Empty;
        try
        {
            var id = await _stock.CreateStockItemAsync().ConfigureAwait(true);
            var imagePath = string.IsNullOrWhiteSpace(imagePathOptional)
                ? null
                : _productImages.EnsureItemIdFileName(imagePathOptional.Trim(), id)
                  ?? imagePathOptional.Trim();
            var bucket = StockCatalogTaxonomy.NormalizeBucket(catalogBucket);
            var sub = StockCatalogTaxonomy.NormalizeSubCategory(bucket, catalogSubCategory);
            var mode = StockCatalogTaxonomy.NormalizeStockMode(stockMode);
            StockCatalogTaxonomy.ApplyStockModeToFlags(mode, out var tr, out var ordIn, out var ngb, out var incWeekly, out var runOut);
            var write = new StockItemAdminWrite
            {
                ItemId = id,
                Name = trimmedName,
                Sku = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim(),
                StockQty = startingStock,
                TrackStock = tr,
                ImagePath = imagePath,
                IsActive = 1,
                StockMovementReference = "Add item wizard",
                CostPrice = null,
                LowStockThreshold = lowVal,
                UsesOpenPrice = usesOpenPriceAtBar ? 1 : 0,
                ShowInPitstop = showInPitstop ? 1 : 0,
                ShowInBar = showInBar ? 1 : 0,
                OrderInMerchandise = ordIn,
                CatalogBucket = bucket,
                CatalogSubCategory = sub,
                StockMode = mode,
                NotGonnaOrderBack = ngb,
                IncludeInWeeklyStockReport = incWeekly,
                IsRunOutItem = runOut,
                IsSharedItem = bucket == StockCatalogTaxonomy.BucketShared ? 1 : 0,
                IsOnSpecial = 0,
                AlternateSkusJson = null,
                ItemDescription = null,
                CategoryId = null,
            };
            await _stock.UpdateItemAdminAsync(write, CancellationToken.None).ConfigureAwait(true);

            await _persist.UpsertPriceIfParsedAsync(id, "Bar", barPriceText, CancellationToken.None).ConfigureAwait(true);
            await _persist.UpsertPriceIfParsedAsync(id, "Pitstop", pitstopPriceText, CancellationToken.None).ConfigureAwait(true);

            BrowserFilterIndex = DefaultFilterIndex;
            SearchText = string.Empty;
            await LoadAsync(resetStatusMessage: false).ConfigureAwait(true);
            TrySelectBrowserRowByItemId(id);
            StatusMessage = "Item created.";
            PulseCatalogRefresh();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void TrySelectBrowserRowByItemId(long itemId)
    {
        var rowVm = PageRows.FirstOrDefault(r => r.ItemId == itemId);
        if (rowVm is not null)
        {
            SelectedPageRow = rowVm;
        }
    }

    private static int? ParseOptionalInt(string? text)
    {
        var raw = (text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    public async Task<bool> ApplySelectedStockAdjustmentAsync()
    {
        if (!HasSelection || IsBusy || SelectedItemId is not long id)
        {
            return false;
        }

        var src = _all.FirstOrDefault(r => r.ItemId == id);
        if (src is null || !StockCatalogTaxonomy.MaintainsOnHandQuantity(src.TrackStock, src.OrderInMerchandise))
        {
            StatusMessage = "This item does not track on-hand stock.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(AdjustReason))
        {
            StatusMessage = "Enter a reason for this stock adjustment.";
            return false;
        }

        if (_adjustQuantity <= 0)
        {
            StatusMessage = "Enter an adjustment quantity greater than zero.";
            return false;
        }

        IsBusy = true;
        try
        {
            var qty = src.StockQty;
            var delta = _adjustQuantity;
            qty = _adjustMode switch
            {
                "Decrease" => Math.Max(0, qty - delta),
                "Set To" => delta,
                _ => qty + delta,
            };

            var snap = StockItemDetailSnapshot.FromRow(src);
            SelectedItemId = id;
            DetailStockText = qty.ToString(CultureInfo.InvariantCulture);
            DetailName = snap.Name;
            DetailSku = snap.Sku;
            DetailTrackStock = snap.TrackStock;
            DetailStockMode = snap.StockMode;
            DetailCatalogBucket = snap.CatalogBucket;
            DetailCatalogSubCategory = snap.CatalogSubCategory;
            DetailOrderInMerchandise = snap.OrderInMerchandise;
            DetailIsActive = snap.IsActive;
            ShowInBar = snap.ShowInBar;
            ShowInPitstop = snap.ShowInPitstop;
            StockAdjustmentReason = AdjustReason;
            await PersistFullItemAsync(AdjustReason).ConfigureAwait(true);
            CurrentScreen = StockManagementScreen.Home;
            StatusMessage = "Stock updated.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> ReceiveStockAsync(
        long itemId,
        int packsBought,
        int packSize,
        decimal totalPaid)
    {
        if (IsBusy || packsBought <= 0 || packSize <= 0 || totalPaid < 0m)
        {
            StatusMessage = "Enter packs bought, pack size, and total paid.";
            return false;
        }

        var src = _all.FirstOrDefault(r => r.ItemId == itemId);
        if (src is null)
        {
            StatusMessage = "Item not found.";
            return false;
        }

        var totalItems = packsBought * packSize;
        var costEach = totalItems > 0 ? (double)(totalPaid / totalItems) : 0d;
        var newQty = src.StockQty + totalItems;
        var purchase = new StockPurchaseWrite
        {
            ItemId = itemId,
            PacksBought = packsBought,
            ItemsPerPack = packSize,
            TotalItems = totalItems,
            TotalPaid = totalPaid,
            CostEach = (decimal)costEach,
        };

        IsBusy = true;
        try
        {
            await _stock.ReceiveStockAsync(purchase, newQty, costEach).ConfigureAwait(true);

            await LoadAsync(resetStatusMessage: false).ConfigureAwait(true);
            StatusMessage = $"Received {totalItems} items.";
            PulseCatalogRefresh();
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> ApplyStockCountResultsAsync(
        IReadOnlyList<(long ItemId, int NewQty)> changes,
        IReadOnlyList<long>? countedItemIds = null)
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            await _stock.ApplyStockCountAsync(changes, countedItemIds).ConfigureAwait(true);
            await LoadAsync(resetStatusMessage: false).ConfigureAwait(true);
            StatusMessage = $"Stock count saved ({changes.Count} change{(changes.Count == 1 ? string.Empty : "s")}).";
            PulseCatalogRefresh();
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
