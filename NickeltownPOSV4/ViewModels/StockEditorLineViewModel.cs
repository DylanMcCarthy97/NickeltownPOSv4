using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels;

public sealed class StockEditorLineViewModel : ObservableViewModel
{
    private readonly StockEditorPanelViewModel _host;

    private readonly IStockEditingService _stock;

    private readonly IInputOverlayService _inputOverlay;

    private string _qtyText;

    private bool _trackStock;

    private string _lineStatus = string.Empty;

    private readonly string _catalogDisplay;

    private readonly StockEditorRow _sourceRow;

    private string _filterBucket;

    public StockEditorLineViewModel(
        StockEditorPanelViewModel host,
        IStockEditingService stock,
        IInputOverlayService inputOverlay,
        StockEditorRow row)
    {
        _host = host;
        _stock = stock;
        _inputOverlay = inputOverlay;
        _sourceRow = row;
        ItemId = row.ItemId;
        DisplayName = row.Name;
        _qtyText = row.StockQty.ToString(CultureInfo.InvariantCulture);
        _trackStock = row.TrackStock != 0;
        _filterBucket = GetFilterBucket(row);
        _catalogDisplay = string.IsNullOrWhiteSpace(row.CategoryName) ? "—" : row.CategoryName.Trim();
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        BeginQtyEntryCommand = new AsyncRelayCommand(BeginQtyEntryAsync);
    }

    public long ItemId { get; }

    public string DisplayName { get; }

    public string CatalogDisplay => _catalogDisplay;

    public string FilterBucket
    {
        get => _filterBucket;
        private set => SetProperty(ref _filterBucket, value);
    }

    public string QtyText
    {
        get => _qtyText;
        private set
        {
            if (SetProperty(ref _qtyText, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool TrackStock
    {
        get => _trackStock;
        set => SetProperty(ref _trackStock, value);
    }

    public string LineStatus
    {
        get => _lineStatus;
        private set => SetProperty(ref _lineStatus, value);
    }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand BeginQtyEntryCommand { get; }

    private static string GetFilterBucket(StockEditorRow row) =>
        StockCatalogTaxonomy.CatalogDisplayName(row.CatalogBucket, row.CatalogSubCategory);

    private async Task BeginQtyEntryAsync()
    {
        var initial = int.TryParse(QtyText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var q) ? q : 0;
        var result = await _inputOverlay
            .ShowNumpadAsync(initial, $"Quantity — {DisplayName}", false, CancellationToken.None)
            .ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        var v = (int)decimal.Truncate(decimal.Round(result.Value, 0, MidpointRounding.AwayFromZero));
        if (v < 0)
        {
            v = 0;
        }

        QtyText = v.ToString(CultureInfo.InvariantCulture);
    }

    private bool CanSave() => int.TryParse(QtyText, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    private async Task SaveAsync()
    {
        if (!int.TryParse(QtyText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var q))
        {
            LineStatus = "Invalid quantity.";
            return;
        }

        LineStatus = "Saving…";
        try
        {
            var mode = string.IsNullOrWhiteSpace(_sourceRow.StockMode)
                ? StockCatalogTaxonomy.StockModeFromLegacyFlags(
                    _sourceRow.TrackStock,
                    _sourceRow.OrderInMerchandise,
                    _sourceRow.NotGonnaOrderBack)
                : StockCatalogTaxonomy.NormalizeStockMode(_sourceRow.StockMode);
            if (!TrackStock && mode == StockCatalogTaxonomy.StockModeTracked)
            {
                mode = StockCatalogTaxonomy.StockModeNotTracked;
            }
            else if (TrackStock && mode == StockCatalogTaxonomy.StockModeNotTracked)
            {
                mode = StockCatalogTaxonomy.StockModeTracked;
            }

            StockCatalogTaxonomy.ApplyStockModeToFlags(mode, out var tr, out var ordIn, out var ngb, out var incWeekly, out var runOut);
            var bucket = StockCatalogTaxonomy.NormalizeBucket(_sourceRow.CatalogBucket);
            var sub = StockCatalogTaxonomy.NormalizeSubCategory(bucket, _sourceRow.CatalogSubCategory);
            await _stock.UpdateItemAdminAsync(
                new StockItemAdminWrite
                {
                    ItemId = ItemId,
                    Name = _sourceRow.Name,
                    Sku = _sourceRow.Sku,
                    StockQty = q,
                    TrackStock = tr,
                    ImagePath = _sourceRow.ImagePath,
                    IsActive = _sourceRow.IsActive,
                    StockMovementReference = "StockEditorPanel",
                    CostPrice = _sourceRow.CostPrice,
                    LowStockThreshold = _sourceRow.LowStockThreshold,
                    UsesOpenPrice = _sourceRow.UsesOpenPrice,
                    ShowInPitstop = _sourceRow.ShowInPitstop,
                    ShowInBar = _sourceRow.ShowInBar,
                    OrderInMerchandise = ordIn,
                    CatalogBucket = bucket,
                    CatalogSubCategory = sub,
                    StockMode = mode,
                    NotGonnaOrderBack = ngb,
                    IncludeInWeeklyStockReport = incWeekly,
                    IsRunOutItem = runOut,
                    IsSharedItem = _sourceRow.IsSharedItem,
                    IsOnSpecial = _sourceRow.IsOnSpecial,
                    AlternateSkusJson = _sourceRow.AlternateSkusJson,
                    ItemDescription = _sourceRow.ItemDescription,
                    CategoryId = null,
                },
                CancellationToken.None).ConfigureAwait(true);
            _sourceRow.StockQty = q;
            _sourceRow.TrackStock = tr;
            _sourceRow.StockMode = mode;
            LineStatus = "Saved.";
            FilterBucket = _catalogDisplay;
            _host.ApplyFilter();
            _host.SetStatus($"Updated {DisplayName}.");
        }
        catch (Exception ex)
        {
            LineStatus = ex.Message;
        }
    }
}

public sealed class StockEditorPanelViewModel : ObservableViewModel
{
    private readonly IStockEditingService _stock;

    private readonly ISlidePanelService _slide;

    private readonly IInputOverlayService _inputOverlay;

    private readonly List<StockEditorLineViewModel> _masterLines = new();

    private string _panelStatus = string.Empty;

    private string _selectedFilter = "All";

    public StockEditorPanelViewModel(
        IStockEditingService stock,
        ISlidePanelService slide,
        IInputOverlayService inputOverlay)
    {
        _stock = stock;
        _slide = slide;
        _inputOverlay = inputOverlay;
        CloseCommand = new RelayCommand(Close);

        if (DispatcherQueue.GetForCurrentThread() is { } dq)
        {
            _ = dq.TryEnqueue(async () => await LoadAsync().ConfigureAwait(true));
        }
        else
        {
            _ = LoadAsync();
        }
    }

    public ObservableCollection<StockEditorLineViewModel> Lines { get; } = new();

    public ObservableCollection<string> FilterChoices { get; } = new();

    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? "All" : value;
            if (!SetProperty(ref _selectedFilter, v))
            {
                return;
            }

            ApplyFilter();
        }
    }

    public string PanelStatus
    {
        get => _panelStatus;
        private set => SetProperty(ref _panelStatus, value);
    }

    public IRelayCommand CloseCommand { get; }

    public void SetStatus(string message) => PanelStatus = message;

    public void ApplyFilter()
    {
        Lines.Clear();
        foreach (var line in _masterLines.Where(MatchesFilter))
        {
            Lines.Add(line);
        }
    }

    private bool MatchesFilter(StockEditorLineViewModel line)
    {
        if (string.Equals(_selectedFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(_selectedFilter, StockCatalogTaxonomy.BucketBar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(_selectedFilter, StockCatalogTaxonomy.BucketPitstop, StringComparison.OrdinalIgnoreCase)
            || string.Equals(_selectedFilter, StockCatalogTaxonomy.BucketShared, StringComparison.OrdinalIgnoreCase))
        {
            return line.FilterBucket.StartsWith(_selectedFilter + " /", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(line.FilterBucket, _selectedFilter, StringComparison.OrdinalIgnoreCase);
    }

    public async Task LoadAsync()
    {
        _masterLines.Clear();
        Lines.Clear();
        PanelStatus = string.Empty;

        try
        {
            await ReloadCategoryChoicesAsync().ConfigureAwait(true);

            var rows = await _stock.GetStockRowsAsync().ConfigureAwait(true);
            foreach (var r in rows)
            {
                _masterLines.Add(new StockEditorLineViewModel(this, _stock, _inputOverlay, r));
            }

            ApplyFilter();

            if (_masterLines.Count == 0)
            {
                PanelStatus = "No active items in SQLite.";
            }
        }
        catch (Exception ex)
        {
            PanelStatus = $"Could not load stock: {ex.Message}";
        }
    }

    private Task ReloadCategoryChoicesAsync()
    {
        RebuildFilterChoices();
        return Task.CompletedTask;
    }

    private void RebuildFilterChoices()
    {
        var keep = _selectedFilter;
        FilterChoices.Clear();
        foreach (var label in StockCatalogTaxonomy.AllFilterCategoryLabels())
        {
            FilterChoices.Add(label);
        }

        if (!FilterChoices.Contains(keep))
        {
            _selectedFilter = "All";
            OnPropertyChanged(nameof(SelectedFilter));
        }

        ApplyFilter();
    }

    private void Close()
    {
        _inputOverlay.Close();
        _slide.Close();
    }
}
