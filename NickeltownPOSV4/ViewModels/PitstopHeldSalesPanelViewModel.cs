using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Pitstop;

namespace NickeltownPOSV4.ViewModels;

public sealed class PitstopHeldSalesPanelViewModel : ObservableViewModel
{
    private readonly IPitstopHeldSaleRepository _heldSales;

    private readonly IPitstopRetailCartHost _cartHost;

    private readonly ISlidePanelService _slide;

    private PitstopHeldSaleRowViewModel? _selected;

    private string _statusMessage = string.Empty;

    private bool _isBusy;

    public PitstopHeldSalesPanelViewModel(
        IPitstopHeldSaleRepository heldSales,
        IPitstopRetailCartHost cartHost,
        ISlidePanelService slide)
    {
        _heldSales = heldSales;
        _cartHost = cartHost;
        _slide = slide;

        Items = new ObservableCollection<PitstopHeldSaleRowViewModel>();

        CloseCommand = new RelayCommand(ClosePanel);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        RecallCommand = new AsyncRelayCommand(RecallAsync, () => !IsBusy && Selected is not null);
        DiscardCommand = new AsyncRelayCommand(DiscardAsync, () => !IsBusy && Selected is not null);

        _ = LoadAsync();
    }

    public ObservableCollection<PitstopHeldSaleRowViewModel> Items { get; }

    public PitstopHeldSaleRowViewModel? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                RecallCommand.NotifyCanExecuteChanged();
                DiscardCommand.NotifyCanExecuteChanged();
            }
        }
    }

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

    public string PageLabel { get; private set; } = "0 held";

    public IRelayCommand CloseCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand RecallCommand { get; }

    public IAsyncRelayCommand DiscardCommand { get; }

    private void ClosePanel() => _slide.Close();

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var rows = await _heldSales.ListHeldSalesAsync().ConfigureAwait(true);
            Items.Clear();
            foreach (var r in rows)
            {
                Items.Add(
                    new PitstopHeldSaleRowViewModel(
                        r.Id,
                        r.HeldAt,
                        r.LineCount,
                        r.TotalAmount,
                        r.StaffDisplayName));
            }

            PageLabel = rows.Count == 0
                ? "No held sales"
                : $"{rows.Count} held sale{(rows.Count == 1 ? "" : "s")}";
            OnPropertyChanged(nameof(PageLabel));

            if (Selected is not null && Items.All(i => i.Id != Selected.Id))
            {
                Selected = null;
            }
        }
        finally
        {
            IsBusy = false;
            RefreshCommand.NotifyCanExecuteChanged();
            RecallCommand.NotifyCanExecuteChanged();
            DiscardCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task RecallAsync()
    {
        if (Selected is null)
        {
            return;
        }

        if (_cartHost.HasActiveCart)
        {
            StatusMessage = "Hold or clear the current sale before recalling.";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var result = await _cartHost.RecallHeldSaleAsync(Selected.Id).ConfigureAwait(true);
            if (!result.Ok)
            {
                StatusMessage = result.ErrorMessage ?? "Could not recall that sale.";
                await LoadAsync().ConfigureAwait(true);
                return;
            }

            _slide.Close();
        }
        finally
        {
            IsBusy = false;
            RecallCommand.NotifyCanExecuteChanged();
            DiscardCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task DiscardAsync()
    {
        if (Selected is null)
        {
            return;
        }

        var id = Selected.Id;
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            await _heldSales.DeleteHeldSaleAsync(id).ConfigureAwait(true);
            await _cartHost.RefreshHeldSaleCountAsync().ConfigureAwait(true);
            Selected = null;
            await LoadAsync().ConfigureAwait(true);
            StatusMessage = "Held sale discarded.";
        }
        finally
        {
            IsBusy = false;
            RecallCommand.NotifyCanExecuteChanged();
            DiscardCommand.NotifyCanExecuteChanged();
        }
    }

    private bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                RecallCommand.NotifyCanExecuteChanged();
                DiscardCommand.NotifyCanExecuteChanged();
            }
        }
    }
}
