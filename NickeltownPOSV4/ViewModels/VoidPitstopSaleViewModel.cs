using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Audit;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels;

public sealed class VoidableSaleRowVm
{
    private static readonly CultureInfo Cult = CultureInfo.CurrentCulture;

    public VoidableSaleRowVm(PitstopActiveSaleRow row, Action<long> requestVoid)
    {
        SaleId = row.SaleId;
        SoldAtText = row.SoldAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", Cult);
        TotalText = row.Total.ToString("C2", Cult);
        PaymentMethod = row.PaymentMethod;
        StaffDisplayName = string.IsNullOrWhiteSpace(row.StaffDisplayName) ? "-" : row.StaffDisplayName;
        IsSquareSale = !string.IsNullOrWhiteSpace(row.SquareExternalRef)
            || row.PaymentMethod.Equals("Square", StringComparison.OrdinalIgnoreCase)
            || row.PaymentMethod.Equals("Card", StringComparison.OrdinalIgnoreCase);
        StockNote = row.StockWasDeducted ? "Stock will be restored" : "No stock to restore";
        VoidCommand = new RelayCommand(() => requestVoid(SaleId));
    }

    public long SaleId { get; }

    public string SoldAtText { get; }

    public string TotalText { get; }

    public string PaymentMethod { get; }

    public string StaffDisplayName { get; }

    public bool IsSquareSale { get; }

    public string StockNote { get; }

    public IRelayCommand VoidCommand { get; }
}

public sealed class VoidPitstopSaleViewModel : ObservableViewModel
{
    private readonly IPitstopRetailSaleRepository _sales;
    private readonly IUserSessionService _session;
    private readonly INavigationService _navigation;
    private readonly IInputOverlayService _input;
    private readonly IWindowHandleProvider _windowHandle;
    private readonly IAuditLogService _audit;

    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public VoidPitstopSaleViewModel(
        IPitstopRetailSaleRepository sales,
        IUserSessionService session,
        INavigationService navigation,
        IInputOverlayService input,
        IWindowHandleProvider windowHandle,
        IAuditLogService audit)
    {
        _sales = sales;
        _session = session;
        _navigation = navigation;
        _input = input;
        _windowHandle = windowHandle;
        _audit = audit;

        Rows = new ObservableCollection<VoidableSaleRowVm>();
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        BackCommand = new RelayCommand(() => _navigation.TryGoBack());
    }

    public ObservableCollection<VoidableSaleRowVm> Rows { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand BackCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public async Task InitializeAsync()
    {
        if (!_session.IsManager)
        {
            StatusMessage = "Admin/Treasurer access required.";
            try
            {
                await _audit.LogAsync(
                    AuditActions.PermissionDenied,
                    AuditEntityTypes.PitstopSale,
                    reason: "Void Sale page requires Admin/Treasurer.",
                    success: false).ConfigureAwait(true);
            }
            catch
            {
                // ignore audit failures
            }

            _navigation.TryGoBack();
            return;
        }

        await LoadAsync().ConfigureAwait(true);
    }

    public async Task LoadAsync()
    {
        if (!_session.IsManager)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var data = await _sales.GetActivePitstopSalesAsync().ConfigureAwait(true);
            Rows.Clear();
            foreach (var row in data)
            {
                Rows.Add(new VoidableSaleRowVm(row, RequestVoid));
            }

            StatusMessage = Rows.Count == 0
                ? "No active Pitstop sales found."
                : $"{Rows.Count} active sale(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void RequestVoid(long saleId)
    {
        var row = Rows.FirstOrDefault(r => r.SaleId == saleId);
        if (row is null)
        {
            return;
        }

        if (!_session.IsManager)
        {
            return;
        }

        var proceed = await ConfirmVoidAsync(row).ConfigureAwait(true);
        if (!proceed)
        {
            return;
        }

        var reason = await _input.ShowKeyboardAsync(string.Empty, "Reason for voiding this sale", CancellationToken.None).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(reason))
        {
            StatusMessage = "Void cancelled: a reason is required.";
            return;
        }

        try
        {
            IsBusy = true;
            var result = await _sales.VoidPitstopSaleAsync(new PitstopVoidSaleRequest
            {
                SaleId = saleId,
                StaffId = _session.ActiveStaffId,
                StaffDisplayName = _session.DisplayName,
                Reason = reason.Trim(),
            }).ConfigureAwait(true);

            if (!result.Ok)
            {
                StatusMessage = result.ErrorMessage ?? "Could not void sale.";
                try
                {
                    await _audit.LogAsync(
                        AuditActions.SaleVoided,
                        AuditEntityTypes.PitstopSale,
                        entityId: saleId.ToString(CultureInfo.InvariantCulture),
                        reason: result.ErrorMessage,
                        success: false).ConfigureAwait(true);
                }
                catch
                {
                    // ignore audit failures
                }

                return;
            }

            try
            {
                await _audit.LogAsync(
                    AuditActions.SaleVoided,
                    AuditEntityTypes.PitstopSale,
                    entityId: saleId.ToString(CultureInfo.InvariantCulture),
                    amount: result.AmountVoided,
                    reason: reason.Trim()).ConfigureAwait(true);

                if (result.StockRestored)
                {
                    await _audit.LogAsync(
                        AuditActions.StockRestored,
                        AuditEntityTypes.PitstopSale,
                        entityId: saleId.ToString(CultureInfo.InvariantCulture),
                        reason: "Stock restored after void.").ConfigureAwait(true);
                }
            }
            catch
            {
                // ignore audit failures
            }

            if (result.WasSquareSale)
            {
                await ShowMessageAsync(
                    "Square card sale voided",
                    "This void only changes POS records and stock. Refunds must be handled in Square.").ConfigureAwait(true);
            }

            await LoadAsync().ConfigureAwait(true);

            StatusMessage = result.StockRestored
                ? "Sale voided. Stock restored."
                : "Sale voided. No stock changes were needed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Void failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> ConfirmVoidAsync(VoidableSaleRowVm row)
    {
        var xamlRoot = _windowHandle.GetXamlRoot();
        if (xamlRoot is null)
        {
            return false;
        }

        var squareWarning = row.IsSquareSale
            ? "\n\nNote: this void only changes POS records and stock. Refunds must be handled in Square."
            : string.Empty;

        var stockNote = string.IsNullOrEmpty(row.StockNote) ? string.Empty : $"\n\n{row.StockNote}.";

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Void this Pitstop sale?",
            Content = new TextBlock
            {
                Text =
                    $"Sale {row.SaleId} - {row.TotalText} ({row.PaymentMethod}) on {row.SoldAtText}.{stockNote}{squareWarning}",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
            },
            PrimaryButtonText = "Void sale",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        var res = await dlg.ShowAsync().AsTask().ConfigureAwait(true);
        return res == ContentDialogResult.Primary;
    }

    private async Task ShowMessageAsync(string title, string text)
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
            Content = new TextBlock
            {
                Text = text,
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
            },
            CloseButtonText = "OK",
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        await dlg.ShowAsync().AsTask().ConfigureAwait(true);
    }
}
