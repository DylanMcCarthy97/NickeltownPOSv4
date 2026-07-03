using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.AddDrinks;

namespace NickeltownPOSV4.ViewModels;

public sealed class AddDrinksPanelViewModel : ObservableViewModel
{
    public const int CatalogColumns = 3;

    /// <summary>3x2 paginated grid - larger readable cards, no product scroll (TCxWave).</summary>
    public const int CatalogRows = 2;

    public const int CatalogPageSize = CatalogColumns * CatalogRows;

    internal const string AllCategoriesToken = "All";

    /// <summary>Pinned bar items for the active tab (shown as its own chip).</summary>
    public const string FavoritesCategoryLabel = "Favourites";

    private readonly IItemCatalogQuery _catalog;

    private readonly IAddDrinksSession _session;

    private readonly ITabWorkspaceRefreshBus _refreshBus;

    private readonly IAddDrinksWorkspaceNavigator _workspaceNavigator;

    private readonly IInputOverlayService _inputOverlay;

    private readonly ITabBarFavoritesService _barFavorites;

    private readonly IWindowHandleProvider _windowHandle;

    private readonly AddDrinksSaleCommitService _saleCommit;

    private readonly IShotMixerConfigService _shotMixerConfig;

    private readonly ShotMixerCatalogTile _shotMixerCatalogTile = new();

    private readonly List<long> _favoriteOrderedIds = new();

    private readonly List<DrinkCardItem> _allProducts = new();

    private readonly List<DrinkCardItem> _filteredProducts = new();

    private string _selectedCategory = AllCategoriesToken;

    private string _statusMessage = string.Empty;

    private bool _isBusy;

    private string _targetTabTitle = "No tab selected";

    private string _pendingScanCode = string.Empty;

    private int _catalogPage = 1;

    private bool _awaitingOpenPriceEntry;

    public AddDrinksPanelViewModel(
        IItemCatalogQuery catalog,
        IAddDrinksSession session,
        ITabWorkspaceRefreshBus refreshBus,
        IAddDrinksWorkspaceNavigator workspaceNavigator,
        IInputOverlayService inputOverlay,
        ITabBarFavoritesService barFavorites,
        IWindowHandleProvider windowHandle,
        AddDrinksSaleCommitService saleCommit,
        IShotMixerConfigService shotMixerConfig)
    {
        _catalog = catalog;
        _session = session;
        _refreshBus = refreshBus;
        _workspaceNavigator = workspaceNavigator;
        _inputOverlay = inputOverlay;
        _barFavorites = barFavorites;
        _windowHandle = windowHandle;
        _saleCommit = saleCommit;
        _shotMixerConfig = shotMixerConfig;

        SelectCategoryCommand = new RelayCommand<string>(SelectCategory);
        AddDrinkCommand = new AsyncRelayCommand<DrinkCardItem>(AddDrinkAsync, CanAddDrinkFromCatalog);
        IncrementLineCommand = new RelayCommand<SelectedDrinkRow>(IncrementLine);
        DecrementLineCommand = new RelayCommand<SelectedDrinkRow>(DecrementLine);
        RemoveLineCommand = new RelayCommand<SelectedDrinkRow>(RemoveLine);
        ClearCartCommand = new AsyncRelayCommand(ClearCartAsync, () => SelectedRows.Count > 0 && !IsBusy);
        CancelCommand = new RelayCommand(ClosePanel);
        AddToTabCommand = new AsyncRelayCommand(CommitAsync, CanCommit);
        CatalogPrevCommand = new RelayCommand(CatalogPrev, () => _catalogPage > 1);
        CatalogNextCommand = new RelayCommand(
            CatalogNext,
            () => _catalogPage < CatalogTotalPages);
        ToggleFavoriteCommand = new RelayCommand<DrinkCardItem>(item => _ = ToggleFavoriteAsync(item), item => !IsBusy && item is not null);
        ShotMixerCommand = new AsyncRelayCommand(RunShotMixerFlowAsync, () => !IsBusy);

        SelectedRows.CollectionChanged += OnSelectedRowsChanged;
        _refreshBus.RefreshRequested += OnCatalogRefreshRequested;

        RefreshTargetTabTitle();

        if (DispatcherQueue.GetForCurrentThread() is { } dq)
        {
            _ = dq.TryEnqueue(async () => await LoadCatalogAsync().ConfigureAwait(true));
        }
        else
        {
            _ = LoadCatalogAsync();
        }
    }

    private void OnSelectedRowsChanged(object? sender, NotifyCollectionChangedEventArgs e) => TouchSelection();

    private void OnCatalogRefreshRequested(object? sender, EventArgs e) => ScheduleCatalogRefresh();

    /// <summary>Reload bar catalog from SQLite (safe to call when the panel is shown).</summary>
    public Task RefreshCatalogFromDatabaseAsync()
    {
        ScheduleCatalogRefresh();
        return Task.CompletedTask;
    }

    private void ScheduleCatalogRefresh()
    {
        if (IsBusy)
        {
            return;
        }

        if (DispatcherQueue.GetForCurrentThread() is { } dq)
        {
            dq.TryEnqueue(async () => await LoadCatalogAsync().ConfigureAwait(true));
            return;
        }

        _ = LoadCatalogAsync();
    }

    public ObservableCollection<CategoryChipItem> CategoryChips { get; } = new();

    /// <summary>
    /// Current catalog page tiles (3x2 paginated grid). Each item is either a
    /// <see cref="DrinkCardItem"/> or <see cref="ShotMixerCatalogTile"/> sorted with products.
    /// </summary>
    public ObservableCollection<object> CatalogPageItems { get; } = new();

    public bool ShowShotMixerOnCatalogPage =>
        ShowShotMixerTile && CatalogPageItems.OfType<ShotMixerCatalogTile>().Any();

    public ObservableCollection<SelectedDrinkRow> SelectedRows { get; } = new();

    public string SelectedCategory
    {
        get => _selectedCategory;
        private set => SetProperty(ref _selectedCategory, value);
    }

    public bool IsFavoritesCategorySelected =>
        string.Equals(SelectedCategory, FavoritesCategoryLabel, StringComparison.Ordinal);

    public bool IsAllCategorySelected =>
        string.Equals(SelectedCategory, AllCategoriesToken, StringComparison.Ordinal);

    public int CatalogPage
    {
        get => _catalogPage;
        private set
        {
            if (SetProperty(ref _catalogPage, value))
            {
                OnPropertyChanged(nameof(CatalogPageLabel));
                OnPropertyChanged(nameof(CanCatalogPrev));
                OnPropertyChanged(nameof(CanCatalogNext));
                OnPropertyChanged(nameof(ShowShotMixerOnCatalogPage));
                CatalogPrevCommand.NotifyCanExecuteChanged();
                CatalogNextCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int CatalogTotalPages =>
        AddDrinksCatalogPager.TotalObjectPages(CatalogEntryCount, CatalogPageSize);

    public string CatalogPageLabel =>
        CatalogTotalPages <= 0
            ? string.Empty
            : $"Page {CatalogPage} of {CatalogTotalPages}";

    public bool ShowCatalogPagination => CatalogTotalPages > 1;

    public bool CanCatalogPrev => CatalogPage > 1;

    public bool CanCatalogNext => CatalogPage < CatalogTotalPages;

    public bool ShowShotMixerTile => AddDrinksCatalogFilter.ShowShotMixerQuickAction(SelectedCategory);

    private int CatalogEntryCount =>
        _filteredProducts.Count + (ShowShotMixerTile ? 1 : 0);

    public bool IsCatalogEmpty => CatalogEntryCount == 0;

    public string CatalogEmptyMessage => "No items in this category.";

    public string HeaderCartChipSummary =>
        $"{SelectedRows.Count} line{(SelectedRows.Count == 1 ? string.Empty : "s")} \u00B7 {TotalQuantity} unit{(TotalQuantity == 1 ? string.Empty : "s")} \u00B7 {CartSubtotal.ToString("C2", CultureInfo.CurrentCulture)}";

    public string TargetTabTitle
    {
        get => _targetTabTitle;
        private set => SetProperty(ref _targetTabTitle, value);
    }

    /// <summary>Tab display name for full-screen workspace header.</summary>
    public string WorkspaceTabDisplayName =>
        AddDrinksTabDisplayHelper.FormatWorkspaceTabDisplayName(
            _session.TargetTabLegacyId,
            _session.TargetTabDisplayName);

    public decimal? CurrentTabBalance => _session.TargetTabBalance;

    public decimal ProjectedBalanceAfterCart => (CurrentTabBalance ?? 0m) - CartSubtotal;

    public bool IsProjectedBalanceNegative => !IsCartEmpty && ProjectedBalanceAfterCart < 0m;

    /// <summary>Balance line for workspace header (from session when workspace opens).</summary>
    public string WorkspaceBalanceDisplay =>
        AddDrinksTabDisplayHelper.FormatWorkspaceBalanceDisplay(CurrentTabBalance, CartSubtotal, IsCartEmpty);

    public string WorkspaceSubtitle => _session.TargetTabIsGuest ? "Guest tab" : "Member bar";

    public bool CanAddToTab => CanCommit();

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

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                AddDrinkCommand.NotifyCanExecuteChanged();
                AddToTabCommand.NotifyCanExecuteChanged();
                ClearCartCommand.NotifyCanExecuteChanged();
                ToggleFavoriteCommand.NotifyCanExecuteChanged();
                ShotMixerCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int TotalQuantity => SelectedRows.Sum(r => r.Quantity);

    public string CartLineCountSummary =>
        SelectedRows.Count == 0
            ? "0 lines \u00B7 0 units"
            : $"{SelectedRows.Count} line{(SelectedRows.Count == 1 ? string.Empty : "s")} \u00B7 {TotalQuantity} unit{(TotalQuantity == 1 ? string.Empty : "s")}";

    public decimal CartSubtotal => SelectedRows.Sum(r => r.Quantity * r.UnitPrice);

    public string CartSubtotalText =>
        $"Subtotal  {CartSubtotal.ToString("C2", CultureInfo.CurrentCulture)}";

    public string CartTotalText =>
        $"Total  {CartSubtotal.ToString("C2", CultureInfo.CurrentCulture)}";

    public bool IsCartEmpty => SelectedRows.Count == 0;

    public string PendingScanCode
    {
        get => _pendingScanCode;
        set => SetProperty(ref _pendingScanCode, value ?? string.Empty);
    }

    /// <summary>Append one character from wedge scanner (buffered until Enter).</summary>
    public void AppendScanChar(char ch)
    {
        if (IsBusy || _pendingScanCode.Length >= 40)
        {
            return;
        }

        _pendingScanCode += ch;
        OnPropertyChanged(nameof(PendingScanCode));
    }

    public void BackspacePendingScan()
    {
        if (_pendingScanCode.Length == 0)
        {
            return;
        }

        _pendingScanCode = _pendingScanCode[..^1];
        OnPropertyChanged(nameof(PendingScanCode));
    }

    /// <summary>Commit SKU field or buffered wedge input (Enter).</summary>
    public void CommitPendingScan()
    {
        _ = CommitPendingScanAsync();
    }

    public IRelayCommand<string> SelectCategoryCommand { get; }

    public IAsyncRelayCommand<DrinkCardItem> AddDrinkCommand { get; }

    public IRelayCommand<SelectedDrinkRow> IncrementLineCommand { get; }

    public IRelayCommand<SelectedDrinkRow> DecrementLineCommand { get; }

    public IRelayCommand<SelectedDrinkRow> RemoveLineCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IAsyncRelayCommand AddToTabCommand { get; }

    public IRelayCommand CatalogPrevCommand { get; }

    public IRelayCommand CatalogNextCommand { get; }

    public IRelayCommand<DrinkCardItem> ToggleFavoriteCommand { get; }

    public IAsyncRelayCommand ClearCartCommand { get; }

    public IAsyncRelayCommand ShotMixerCommand { get; }

    public string TotalQuantitySummary => $"Total units: {TotalQuantity}";

    public async Task LoadCatalogAsync()
    {
        StatusMessage = string.Empty;
        RefreshTargetTabTitle();

        try
        {
            var previousCategory = SelectedCategory;

            _allProducts.Clear();
            var products = await _catalog.GetBarProductsAsync(categoryName: null).ConfigureAwait(true);
            var tabId = _session.TargetTabLegacyId ?? string.Empty;
            _favoriteOrderedIds.Clear();
            var manualFavoriteIds = new HashSet<long>();
            if (!string.IsNullOrWhiteSpace(tabId))
            {
                foreach (var id in await _barFavorites
                             .GetManualFavoriteItemIdsOrderedAsync(tabId, CancellationToken.None)
                             .ConfigureAwait(true))
                {
                    manualFavoriteIds.Add(id);
                }

                await RefreshEffectiveFavoriteIdsAsync(tabId).ConfigureAwait(true);
            }

            foreach (var p in products)
            {
                var alts = AddDrinksBarcodeHelper.ParseAlternateSkusJson(p.AlternateSkusJson);
                _allProducts.Add(
                    AddDrinksCatalogProductFactory.FromBarProduct(
                        p,
                        manualFavoriteIds.Contains(p.ItemId),
                        _session.TargetTabIsGuest,
                        alts));
            }

            if (_allProducts.Count == 0)
            {
                StatusMessage = "No priced products in SQLite. Import drinks/items or add prices, then reopen this panel.";
            }

            RebuildCategoryChips();

            SelectedCategory = CategoryChips.FirstOrDefault(c =>
                    string.Equals(c.Label, previousCategory, StringComparison.OrdinalIgnoreCase))?.Label
                ?? AllCategoriesToken;
            NotifyCategorySelectionChanged();
            await RefreshShotMixerCatalogTileAsync().ConfigureAwait(true);
            FilterAndPageCatalog();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load catalog: {ex.Message}";
            CatalogPageItems.Clear();
        }
    }

    private void RefreshTargetTabTitle()
    {
        TargetTabTitle = AddDrinksTabDisplayHelper.FormatTargetTabTitle(
            _session.TargetTabLegacyId,
            _session.TargetTabDisplayName);
        OnPropertyChanged(nameof(WorkspaceTabDisplayName));
        OnPropertyChanged(nameof(WorkspaceBalanceDisplay));
    }

    private bool CanCommit() =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(_session.TargetTabLegacyId)
        && SelectedRows.Count > 0;

    /// <summary>
    /// Current filter label, used in the panel header. "All" is the implicit
    /// default when no category is selected.
    /// </summary>
    public string CurrentFilterLabel =>
        string.IsNullOrWhiteSpace(SelectedCategory) ? AllCategoriesToken : SelectedCategory;

    /// <summary>
    /// Subtitle under the "Products" header: "Showing: All items" when the
    /// implicit-default All filter is active, otherwise "Showing: {category}".
    /// </summary>
    public string CurrentFilterSummary =>
        IsAllCategorySelected ? "Showing: All items" : $"Showing: {CurrentFilterLabel}";

    /// <summary>True when a category filter is active (a Clear-filter button should be visible).</summary>
    public bool HasActiveCategoryFilter => !IsAllCategorySelected;

    private void RebuildCategoryChips()
    {
        CategoryChips.Clear();
        foreach (var label in AddDrinksCatalogFilter.BuildCategoryChipLabels(_allProducts))
        {
            CategoryChips.Add(new CategoryChipItem(label));
        }
    }

    private void SelectCategory(string? category)
    {
        if (string.IsNullOrEmpty(category))
        {
            return;
        }

        SelectedCategory = category;
        NotifyCategorySelectionChanged();
        FilterAndPageCatalog();
    }

    private void NotifyCategorySelectionChanged()
    {
        foreach (var chip in CategoryChips)
        {
            chip.IsSelected = string.Equals(chip.Label, SelectedCategory, StringComparison.OrdinalIgnoreCase);
        }

        OnPropertyChanged(nameof(IsFavoritesCategorySelected));
        OnPropertyChanged(nameof(IsAllCategorySelected));
        OnPropertyChanged(nameof(ShowShotMixerTile));
        OnPropertyChanged(nameof(ShowShotMixerOnCatalogPage));
        OnPropertyChanged(nameof(CurrentFilterLabel));
        OnPropertyChanged(nameof(CurrentFilterSummary));
        OnPropertyChanged(nameof(HasActiveCategoryFilter));
    }

    private void FilterAndPageCatalog()
    {
        _filteredProducts.Clear();
        var filter = AddDrinksCatalogFilter.Filter(
            _allProducts,
            SelectedCategory,
            _favoriteOrderedIds,
            FavoritesCategoryLabel,
            AllCategoriesToken);

        if (filter.FavoritesStatusHint is { } hint)
        {
            StatusMessage = hint;
        }
        else if (StatusMessage.StartsWith("No favourites", StringComparison.Ordinal))
        {
            StatusMessage = string.Empty;
        }

        _filteredProducts.AddRange(filter.Items);
        CatalogPage = 1;
        OnPropertyChanged(nameof(IsCatalogEmpty));
        BindCatalogPage();
    }

    private async Task RefreshShotMixerCatalogTileAsync()
    {
        if (!ShowShotMixerTile)
        {
            return;
        }

        try
        {
            var cfg = await _shotMixerConfig
                .GetAsync(_session.TargetTabIsGuest, CancellationToken.None)
                .ConfigureAwait(true);
            _shotMixerCatalogTile.ApplyFromConfig(cfg);
        }
        catch
        {
            // Keep the last displayed price if config cannot be read.
        }
    }

    private void BindCatalogPage()
    {
        var sortedEntries = AddDrinksCatalogPager.BuildSortedCatalogEntries(
            _filteredProducts,
            ShowShotMixerTile ? _shotMixerCatalogTile : null);
        var clamped = AddDrinksCatalogPager.ClampObjectPage(
            _catalogPage,
            sortedEntries.Count,
            CatalogPageSize);
        if (clamped != _catalogPage)
        {
            _catalogPage = clamped;
            OnPropertyChanged(nameof(CatalogPage));
        }

        CatalogPageItems.Clear();
        foreach (var entry in AddDrinksCatalogPager.GetObjectPage(
                     sortedEntries,
                     _catalogPage,
                     CatalogPageSize))
        {
            CatalogPageItems.Add(entry);
        }

        OnPropertyChanged(nameof(CatalogPageLabel));
        OnPropertyChanged(nameof(CatalogTotalPages));
        OnPropertyChanged(nameof(ShowCatalogPagination));
        OnPropertyChanged(nameof(ShowShotMixerOnCatalogPage));
        OnPropertyChanged(nameof(CanCatalogPrev));
        OnPropertyChanged(nameof(CanCatalogNext));
        OnPropertyChanged(nameof(IsCatalogEmpty));
        CatalogPrevCommand.NotifyCanExecuteChanged();
        CatalogNextCommand.NotifyCanExecuteChanged();
        SyncDrinkCardCartQuantities();
    }

    private void SyncDrinkCardCartQuantities() =>
        AddDrinksCartHelper.SyncCatalogPageQuantities(
            CatalogPageItems.OfType<DrinkCardItem>(),
            SelectedRows);

    private void CatalogPrev()
    {
        if (_catalogPage <= 1)
        {
            return;
        }

        _catalogPage--;
        OnPropertyChanged(nameof(CatalogPage));
        BindCatalogPage();
    }

    private void CatalogNext()
    {
        if (_catalogPage >= CatalogTotalPages)
        {
            return;
        }

        _catalogPage++;
        OnPropertyChanged(nameof(CatalogPage));
        BindCatalogPage();
    }

    private bool CanAddDrinkFromCatalog(DrinkCardItem? item) =>
        item is not null && item.CanAddFromCatalog && !_awaitingOpenPriceEntry && !IsBusy;

    private async Task AddDrinkAsync(DrinkCardItem? item)
    {
        if (item is null || !item.CanAddFromCatalog || IsBusy)
        {
            return;
        }

        decimal unitPrice = item.UnitPrice;
        if (item.UsesOpenPrice)
        {
            _awaitingOpenPriceEntry = true;
            AddDrinkCommand.NotifyCanExecuteChanged();
            try
            {
                var initial = decimal.Round(item.UnitPrice, 2, MidpointRounding.AwayFromZero);
                if (initial < 0m)
                {
                    initial = 0m;
                }

                var entered = await _inputOverlay
                    .ShowNumpadAsync(initial, $"Price: {item.Name}", allowSignedAmount: false)
                    .ConfigureAwait(true);
                if (entered is null)
                {
                    return;
                }

                unitPrice = decimal.Round(entered.Value, 2, MidpointRounding.AwayFromZero);
                if (unitPrice <= 0m)
                {
                    StatusMessage = "Enter a price greater than zero.";
                    return;
                }
            }
            finally
            {
                _awaitingOpenPriceEntry = false;
                AddDrinkCommand.NotifyCanExecuteChanged();
            }
        }

        StatusMessage = string.Empty;
        AddDrinksCartHelper.AddOrIncrementLine(SelectedRows, item, unitPrice, _session);
        TouchSelection();
        await MaybePromptMixerAsync(item).ConfigureAwait(true);
    }

    private async Task CommitPendingScanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var code = (_pendingScanCode ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return;
        }

        await TryAddByScanAsync(code).ConfigureAwait(true);
    }

    public async Task TryAddByScanAsync(string raw)
    {
        if (IsBusy)
        {
            return;
        }

        var code = (raw ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return;
        }

        var hit = AddDrinksBarcodeHelper.FindProduct(_allProducts, code);

        if (hit is null)
        {
            StatusMessage = $"No drink matches \"{code}\". Set SKU / barcode on the item in Stock management.";
            return;
        }

        if (!hit.CanAddFromCatalog)
        {
            StatusMessage = $"\"{hit.Name}\" is out of stock.";
            return;
        }

        StatusMessage = string.Empty;
        PendingScanCode = string.Empty;
        await AddDrinkAsync(hit).ConfigureAwait(true);
    }

    private async Task RunShotMixerFlowAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ShotMixerRuntimeConfig cfg;
        try
        {
            cfg = await _shotMixerConfig
                .GetAsync(_session.TargetTabIsGuest, CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load Shot + Mixer settings: {ex.Message}";
            return;
        }

        var spirit = await PickSpiritForShotMixerAsync(cfg.Spirits).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(spirit))
        {
            return;
        }

        var mixerChoices = AddDrinksMixerHelper.GetShotMixerChoices(_allProducts);
        if (mixerChoices.Count == 0)
        {
            StatusMessage = "No mixers in the bar catalog. Add soft drinks under Bar / Drinks or Bar / Mixers.";
            return;
        }

        if (mixerChoices.All(c => !c.IsEnabled))
        {
            StatusMessage = "All mixers are out of stock.";
            return;
        }

        var pickedId = await PickMixerForShotMixerAsync(spirit, mixerChoices).ConfigureAwait(true);
        if (pickedId is not long mixerId)
        {
            return;
        }

        var mixer = _allProducts.FirstOrDefault(p => p.ItemId == mixerId);
        if (mixer is null)
        {
            return;
        }

        var lineName = ShotMixerOptions.FormatLineName(spirit, mixer.Name);
        StatusMessage = string.Empty;
        AddDrinksCartHelper.AddOrIncrementShotMixerLine(
            SelectedRows,
            mixer.ItemId,
            lineName,
            cfg.ShotPrice,
            _session);
        TouchSelection();
    }

    private async Task MaybePromptMixerAsync(DrinkCardItem primary)
    {
        if (IsBusy || !AddDrinksMixerHelper.LooksLikeShot(primary))
        {
            return;
        }

        var mixerChoices = AddDrinksMixerHelper.GetMixerChoices(_allProducts);
        if (mixerChoices.Count == 0)
        {
            return;
        }

        var pickedId = await PickMixerFromDialogAsync(mixerChoices, primary.Name).ConfigureAwait(true);
        if (pickedId is not long id)
        {
            return;
        }

        var mixer = _allProducts.FirstOrDefault(p => p.ItemId == id);
        if (mixer is not null)
        {
            await AddDrinkAsync(mixer).ConfigureAwait(true);
        }
    }

    private async Task ToggleFavoriteAsync(DrinkCardItem? item)
    {
        if (item is null || IsBusy || string.IsNullOrWhiteSpace(_session.TargetTabLegacyId))
        {
            return;
        }

        var tab = _session.TargetTabLegacyId!;
        var newFavorite = !item.IsFavorite;
        try
        {
            IsBusy = true;
            await _barFavorites
                .SetFavoriteAsync(tab, item.ItemId, newFavorite, CancellationToken.None)
                .ConfigureAwait(true);
            item.IsFavorite = newFavorite;
            await RefreshEffectiveFavoriteIdsAsync(tab).ConfigureAwait(true);

            if (string.Equals(SelectedCategory, FavoritesCategoryLabel, StringComparison.Ordinal))
            {
                FilterAndPageCatalog();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void IncrementLine(SelectedDrinkRow? row)
    {
        if (row is null)
        {
            return;
        }

        row.Quantity++;
        TouchSelection();
    }

    private void DecrementLine(SelectedDrinkRow? row)
    {
        if (row is null)
        {
            return;
        }

        row.Quantity--;
        if (row.Quantity <= 0)
        {
            SelectedRows.Remove(row);
        }

        TouchSelection();
    }

    private void RemoveLine(SelectedDrinkRow? row)
    {
        if (row is null)
        {
            return;
        }

        SelectedRows.Remove(row);
        TouchSelection();
    }

    private async Task ClearCartAsync()
    {
        if (SelectedRows.Count == 0 || IsBusy)
        {
            return;
        }

        var confirmed = await ConfirmAsync(
                "Clear order?",
                "Remove all drinks from this order? This cannot be undone.")
            .ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        SelectedRows.Clear();
        TouchSelection();
    }

    private void TouchSelection()
    {
        OnPropertyChanged(nameof(TotalQuantity));
        OnPropertyChanged(nameof(TotalQuantitySummary));
        OnPropertyChanged(nameof(CartLineCountSummary));
        OnPropertyChanged(nameof(CartSubtotal));
        OnPropertyChanged(nameof(CartSubtotalText));
        OnPropertyChanged(nameof(CartTotalText));
        OnPropertyChanged(nameof(HeaderCartChipSummary));
        OnPropertyChanged(nameof(IsCartEmpty));
        OnPropertyChanged(nameof(ProjectedBalanceAfterCart));
        OnPropertyChanged(nameof(IsProjectedBalanceNegative));
        OnPropertyChanged(nameof(WorkspaceBalanceDisplay));
        OnPropertyChanged(nameof(CanAddToTab));
        SyncDrinkCardCartQuantities();
        AddToTabCommand.NotifyCanExecuteChanged();
        ClearCartCommand.NotifyCanExecuteChanged();
    }

    private void ClosePanel()
    {
        _workspaceNavigator.RequestClose();
        _session.Clear();
    }

    private async Task CommitAsync()
    {
        if (!CanCommit())
        {
            return;
        }

        var projectedAfterSale = ProjectedBalanceAfterCart;

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var lines = AddDrinksCartHelper.ToSaleLines(SelectedRows);
            var result = await _saleCommit
                .CommitAsync(_session.TargetTabLegacyId!, lines, CancellationToken.None)
                .ConfigureAwait(true);

            if (!result.Ok)
            {
                StatusMessage = result.ErrorMessage ?? "Could not add drinks to the tab.";
                return;
            }

            SelectedRows.Clear();
            TouchSelection();
            _workspaceNavigator.RequestClose();
            _session.Clear();

            if (projectedAfterSale < 0m)
            {
                await ShowInfoAsync(
                        "Tab balance is negative",
                        AddDrinksSaleCommitService.FormatNegativeBalanceMessage(projectedAfterSale))
                    .ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
            AddToTabCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task<long?> PickMixerFromDialogAsync(
        IReadOnlyList<MixerPickerChoice> choices,
        string shotName)
    {
        var xamlRoot = _windowHandle.GetXamlRoot();
        if (xamlRoot is null || choices.Count == 0)
        {
            return null;
        }

        var list = new ListView
        {
            ItemsSource = choices,
            SelectionMode = ListViewSelectionMode.Single,
            MinHeight = 220,
            MaxHeight = 320,
        };

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Add a mixer?",
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = shotName,
                        FontSize = 14,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        TextWrapping = TextWrapping.WrapWholeWords,
                    },
                    list,
                },
            },
            PrimaryButtonText = "Add mixer",
            CloseButtonText = "No mixer",
            DefaultButton = ContentDialogButton.Close,
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        if (await dlg.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        return list.SelectedItem is MixerPickerChoice picked ? picked.ItemId : null;
    }

    private async Task<bool> ConfirmAsync(string title, string body)
    {
        var xamlRoot = _windowHandle.GetXamlRoot();
        if (xamlRoot is null)
        {
            return false;
        }

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = new TextBlock
            {
                Text = body,
                TextWrapping = TextWrapping.WrapWholeWords,
            },
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowInfoAsync(string title, string body)
    {
        var xamlRoot = _windowHandle.GetXamlRoot();
        if (xamlRoot is null)
        {
            return;
        }

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            Content = new TextBlock
            {
                Text = body,
                TextWrapping = TextWrapping.WrapWholeWords,
            },
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        await dlg.ShowAsync();
    }

    private async Task<string?> PickSpiritForShotMixerAsync(IReadOnlyList<string> spirits)
    {
        var xamlRoot = _windowHandle.GetXamlRoot();
        if (xamlRoot is null || spirits.Count == 0)
        {
            return null;
        }

        string? picked = null;
        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Choose Spirit",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        var board = BuildStretchPickerBoard(
            spirits,
            columnsPerRow: 2,
            rowMinHeight: 58,
            (spirit, minHeight) =>
            {
                var btn = CreatePickerChoiceButton(spirit, isEnabled: true, minHeight);
                btn.Click += (_, _) =>
                {
                    picked = spirit;
                    dlg.Hide();
                };
                return btn;
            });

        dlg.Content = WrapPickerDialogBody(board, maxScrollHeight: 420);

        PosContentDialogHelper.ApplyPosStyle(dlg);
        await dlg.ShowAsync();
        return picked;
    }

    private async Task<long?> PickMixerForShotMixerAsync(
        string spiritName,
        IReadOnlyList<MixerPickerChoice> choices)
    {
        var xamlRoot = _windowHandle.GetXamlRoot();
        if (xamlRoot is null || choices.Count == 0)
        {
            return null;
        }

        long? pickedId = null;
        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Choose Mixer",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        var board = BuildStretchPickerBoard(
            choices,
            columnsPerRow: 2,
            rowMinHeight: 78,
            (choice, minHeight) =>
            {
                var btn = CreateMixerPickerButton(choice, minHeight);
                if (choice.IsEnabled)
                {
                    var id = choice.ItemId;
                    btn.Click += (_, _) =>
                    {
                        pickedId = id;
                        dlg.Hide();
                    };
                }

                return btn;
            });

        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(
            new TextBlock
            {
                Text = spiritName,
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.WrapWholeWords,
            });
        body.Children.Add(WrapPickerDialogBody(board, maxScrollHeight: 460));
        dlg.Content = body;

        PosContentDialogHelper.ApplyPosStyle(dlg);
        await dlg.ShowAsync();
        return pickedId;
    }

    private const double ShotMixerPickerBoardWidth = 440;

    private static ScrollViewer WrapPickerDialogBody(UIElement board, double maxScrollHeight) =>
        new()
        {
            MaxHeight = maxScrollHeight,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new Border
            {
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Child = board,
            },
        };

    private static StackPanel BuildStretchPickerBoard<T>(
        IReadOnlyList<T> items,
        int columnsPerRow,
        double rowMinHeight,
        Func<T, double, UIElement> createCell)
    {
        var cols = Math.Max(1, Math.Min(columnsPerRow, items.Count));
        var root = new StackPanel
        {
            Spacing = 10,
            Width = ShotMixerPickerBoardWidth,
            MaxWidth = ShotMixerPickerBoardWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        for (var i = 0; i < items.Count; i += cols)
        {
            var row = new Grid
            {
                MinHeight = rowMinHeight,
                ColumnSpacing = 10,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            for (var c = 0; c < cols; c++)
            {
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            for (var j = 0; j < cols && i + j < items.Count; j++)
            {
                var cell = createCell(items[i + j], rowMinHeight);
                if (cell is FrameworkElement fe)
                {
                    Grid.SetColumn(fe, j);
                    row.Children.Add(fe);
                }
            }

            root.Children.Add(row);
        }

        return root;
    }

    private static Button CreatePickerChoiceButton(
        string label,
        bool isEnabled,
        double minHeight)
    {
        var btn = new Button
        {
            Content = label,
            MinHeight = minHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(10, 8, 10, 8),
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            IsEnabled = isEnabled,
        };

        ApplyStandardPickerButtonChrome(btn, isEnabled);
        return btn;
    }

    private static Button CreateMixerPickerButton(MixerPickerChoice choice, double minHeight)
    {
        var nameBlock = new TextBlock
        {
            Text = choice.Name,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 2,
            TextWrapping = TextWrapping.WrapWholeWords,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var stockBlock = new TextBlock
        {
            Text = choice.StockText,
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var panel = new StackPanel
        {
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children = { nameBlock, stockBlock },
        };

        var btn = new Button
        {
            Content = panel,
            MinHeight = minHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            IsEnabled = choice.IsEnabled,
        };

        ApplyStandardPickerButtonChrome(btn, choice.IsEnabled);
        nameBlock.Foreground = GetThemeBrush("PosTextPrimaryBrush");
        stockBlock.Foreground = GetThemeBrush("PosTextSecondaryBrush");
        if (!choice.IsEnabled)
        {
            btn.Opacity = 0.55;
        }

        return btn;
    }

    /// <summary>Same chrome as other touch buttons in Add Drinks (no accent fill).</summary>
    private static void ApplyStandardPickerButtonChrome(Button btn, bool isEnabled)
    {
        if (Application.Current.Resources.TryGetValue("TabsHeaderActionButtonStyle", out var styleObj)
            && styleObj is Style headerStyle)
        {
            btn.Style = headerStyle;
        }

        btn.UseSystemFocusVisuals = false;
        if (!isEnabled)
        {
            btn.Opacity = 0.55;
        }
    }

    private static Brush GetThemeBrush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var brush) && brush is Brush b
            ? b
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);

    private async Task RefreshEffectiveFavoriteIdsAsync(string tabLegacyId)
    {
        _favoriteOrderedIds.Clear();
        _favoriteOrderedIds.AddRange(
            await AddDrinksFavoritesBlend.LoadEffectiveFavoriteIdsAsync(
                _barFavorites,
                tabLegacyId,
                _session.TargetTabDisplayName,
                _session.SessionFavoriteCounts,
                CancellationToken.None)
                .ConfigureAwait(true));
    }
}
