using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Complimentary;

namespace NickeltownPOSV4.ViewModels.Settings;

public sealed class QuickFreeConfigItemViewModel : ObservableViewModel
{
    public QuickFreeConfigItemViewModel(
        QuickFreeConfigRow row,
        IAsyncRelayCommand<QuickFreeConfigItemViewModel> removeCommand,
        IAsyncRelayCommand<QuickFreeConfigItemViewModel> moveUpCommand,
        IAsyncRelayCommand<QuickFreeConfigItemViewModel> moveDownCommand)
    {
        ItemId = row.ItemId;
        ProductName = row.ProductName;
        SortOrder = row.SortOrder;
        ProductIsActive = row.ProductIsActive != 0;
        RemoveCommand = removeCommand;
        MoveUpCommand = moveUpCommand;
        MoveDownCommand = moveDownCommand;
    }

    public long ItemId { get; }

    public string ProductName { get; }

    public int SortOrder { get; }

    public bool ProductIsActive { get; }

    public string ProductLine => ProductIsActive ? ProductName : ProductName + " (inactive)";

    public IAsyncRelayCommand<QuickFreeConfigItemViewModel> RemoveCommand { get; }

    public IAsyncRelayCommand<QuickFreeConfigItemViewModel> MoveUpCommand { get; }

    public IAsyncRelayCommand<QuickFreeConfigItemViewModel> MoveDownCommand { get; }
}

public sealed class QuickFreeItemsConfigViewModel : SettingsSubViewModelBase
{
    private readonly IComplimentaryItemService _complimentary;
    private readonly IInputOverlayService _inputOverlay;

    private QuickFreeProductCandidate? _selectedCandidate;
    private string _productQuery = string.Empty;

    public QuickFreeItemsConfigViewModel(
        INavigationService navigation,
        IComplimentaryItemService complimentary,
        IInputOverlayService inputOverlay)
        : base(navigation)
    {
        _complimentary = complimentary;
        _inputOverlay = inputOverlay;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        SearchProductCommand = new AsyncRelayCommand(SearchProductAsync, () => !IsBusy);
        SelectCandidateCommand = new RelayCommand<QuickFreeProductCandidate>(SelectCandidate);
        AddCommand = new AsyncRelayCommand(AddAsync, () => !IsBusy && SelectedCandidate is not null);
        RemoveCommand = new AsyncRelayCommand<QuickFreeConfigItemViewModel>(RemoveAsync);
        MoveUpCommand = new AsyncRelayCommand<QuickFreeConfigItemViewModel>(row => MoveAsync(row, -1));
        MoveDownCommand = new AsyncRelayCommand<QuickFreeConfigItemViewModel>(row => MoveAsync(row, 1));
    }

    public ObservableCollection<QuickFreeConfigItemViewModel> ConfiguredItems { get; } = new();

    public ObservableCollection<QuickFreeProductCandidate> Candidates { get; } = new();

    public ObservableCollection<QuickFreeProductCandidate> CandidateSuggestions { get; } = new();

    public string ProductQuery
    {
        get => _productQuery;
        private set
        {
            if (SetProperty(ref _productQuery, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(ProductSearchSummary));
            }
        }
    }

    public string ProductSearchSummary =>
        SelectedCandidate is not null
            ? SelectedCandidate.Name
            : string.IsNullOrWhiteSpace(ProductQuery)
                ? "Tap to search a product"
                : ProductQuery;

    public bool HasSuggestions => CandidateSuggestions.Count > 0;

    public QuickFreeProductCandidate? SelectedCandidate
    {
        get => _selectedCandidate;
        set
        {
            if (SetProperty(ref _selectedCandidate, value))
            {
                AddCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(ProductSearchSummary));
            }
        }
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand SearchProductCommand { get; }

    public IRelayCommand<QuickFreeProductCandidate> SelectCandidateCommand { get; }

    public IAsyncRelayCommand AddCommand { get; }

    public IAsyncRelayCommand<QuickFreeConfigItemViewModel> RemoveCommand { get; }

    public IAsyncRelayCommand<QuickFreeConfigItemViewModel> MoveUpCommand { get; }

    public IAsyncRelayCommand<QuickFreeConfigItemViewModel> MoveDownCommand { get; }

    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var config = await _complimentary.GetConfigAsync().ConfigureAwait(true);
            var candidates = await _complimentary.GetProductCandidatesAsync().ConfigureAwait(true);
            ConfiguredItems.Clear();
            foreach (var row in config)
            {
                ConfiguredItems.Add(new QuickFreeConfigItemViewModel(
                    row,
                    RemoveCommand,
                    MoveUpCommand,
                    MoveDownCommand));
            }

            Candidates.Clear();
            foreach (var row in candidates)
            {
                Candidates.Add(row);
            }

            ProductQuery = string.Empty;
            SelectedCandidate = null;
            CandidateSuggestions.Clear();
            OnPropertyChanged(nameof(HasSuggestions));
            SetStatus(ConfiguredItems.Count == 0
                ? "No Quick Free Items yet. Search for Water and Pop Top, then add them."
                : $"{ConfiguredItems.Count} Quick Free Item(s). Removing a product here does not delete it from stock.");
        }
        finally
        {
            IsBusy = false;
            AddCommand.NotifyCanExecuteChanged();
            SearchProductCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task SearchProductAsync()
    {
        var result = await _inputOverlay
            .ShowKeyboardAsync(ProductQuery, "Search a product")
            .ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        ProductQuery = result.Trim();
        SelectedCandidate = null;
        UpdateSuggestions(ProductQuery);
        if (CandidateSuggestions.Count == 1)
        {
            SelectCandidate(CandidateSuggestions[0]);
        }
    }

    public void SelectCandidate(QuickFreeProductCandidate? candidate)
    {
        if (candidate is null)
        {
            return;
        }

        SelectedCandidate = candidate;
        ProductQuery = candidate.Name;
        CandidateSuggestions.Clear();
        OnPropertyChanged(nameof(HasSuggestions));
    }

    private void UpdateSuggestions(string query)
    {
        var q = (query ?? string.Empty).Trim();
        CandidateSuggestions.Clear();
        if (q.Length > 0)
        {
            foreach (var row in Candidates.Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(40))
            {
                CandidateSuggestions.Add(row);
            }
        }

        OnPropertyChanged(nameof(HasSuggestions));
    }

    private async Task AddAsync()
    {
        if (SelectedCandidate is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _complimentary
                .AddConfigAsync(SelectedCandidate.ItemId, displayLabel: null, icon: null)
                .ConfigureAwait(true);
            if (!result.Ok)
            {
                SetStatus(result.ErrorMessage ?? "Could not add that product.");
                return;
            }

            await RefreshAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            AddCommand.NotifyCanExecuteChanged();
            SearchProductCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task RemoveAsync(QuickFreeConfigItemViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var result = await _complimentary.RemoveConfigAsync(row.ItemId).ConfigureAwait(true);
        if (!result.Ok)
        {
            SetStatus(result.ErrorMessage ?? "Could not remove that product.");
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task MoveAsync(QuickFreeConfigItemViewModel? row, int direction)
    {
        if (row is null)
        {
            return;
        }

        var result = await _complimentary.MoveConfigAsync(row.ItemId, direction).ConfigureAwait(true);
        if (!result.Ok)
        {
            SetStatus(result.ErrorMessage ?? "Could not reorder.");
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
    }
}
