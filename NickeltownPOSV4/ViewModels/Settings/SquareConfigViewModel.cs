using System;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.ViewModels.Settings;

public sealed class SquareConfigViewModel : SettingsSubViewModelBase
{
    private readonly ISquareConfigService _config;
    private readonly IInputOverlayService _inputOverlay;
    private readonly ISquareConfigImportService _import;
    private readonly ISquareConfigFilePicker _filePicker;

    private string _accessToken = string.Empty;
    private string _locationId = string.Empty;
    private string _deviceId = string.Empty;
    private string _environment = "production";
    private string _barTabCardCatalogVariationId = string.Empty;
    private string _guestTabCardCatalogVariationId = string.Empty;
    private double _cardSurchargePercent = 1.7;
    private bool _isSandbox;

    public SquareConfigViewModel(
        INavigationService navigation,
        ISquareConfigService config,
        IInputOverlayService inputOverlay,
        ISquareConfigImportService import,
        ISquareConfigFilePicker filePicker)
        : base(navigation)
    {
        _config = config;
        _inputOverlay = inputOverlay;
        _import = import;
        _filePicker = filePicker;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        ImportFromV2Command = new AsyncRelayCommand(ImportFromV2Async, () => !IsBusy);

        EditAccessTokenCommand = new AsyncRelayCommand(EditAccessTokenAsync, () => !IsBusy);
        EditLocationIdCommand = new AsyncRelayCommand(EditLocationIdAsync, () => !IsBusy);
        EditDeviceIdCommand = new AsyncRelayCommand(EditDeviceIdAsync, () => !IsBusy);
        EditBarTabVariationIdCommand = new AsyncRelayCommand(EditBarTabVariationIdAsync, () => !IsBusy);
        EditGuestTabVariationIdCommand = new AsyncRelayCommand(EditGuestTabVariationIdAsync, () => !IsBusy);
        EditCardSurchargePercentCommand = new AsyncRelayCommand(EditCardSurchargePercentAsync, () => !IsBusy);

        RefreshLegacyPathProbe();
    }

    private string _legacyConfigPath = string.Empty;

    public string LegacyConfigPath
    {
        get => _legacyConfigPath;
        private set
        {
            if (SetProperty(ref _legacyConfigPath, value))
            {
                OnPropertyChanged(nameof(HasLegacyConfig));
                OnPropertyChanged(nameof(LegacyConfigSummary));
            }
        }
    }

    public bool HasLegacyConfig => !string.IsNullOrEmpty(LegacyConfigPath);

    public string LegacyConfigSummary =>
        HasLegacyConfig
            ? $"Auto-detected at: {LegacyConfigPath}. Use Import to choose this file or another square_config.json."
            : "No square_config.json auto-detected. Use Import to choose your legacy square_config.json file.";

    public IAsyncRelayCommand ImportFromV2Command { get; }

    public string AccessToken
    {
        get => _accessToken;
        set
        {
            if (SetProperty(ref _accessToken, value))
            {
                OnPropertyChanged(nameof(AccessTokenSummary));
            }
        }
    }

    public string AccessTokenSummary =>
        string.IsNullOrEmpty(AccessToken) ? "Tap to set access token" : new string('•', Math.Min(AccessToken.Length, 12));

    public string LocationId
    {
        get => _locationId;
        set
        {
            if (SetProperty(ref _locationId, value))
            {
                OnPropertyChanged(nameof(LocationIdSummary));
            }
        }
    }

    public string LocationIdSummary =>
        string.IsNullOrWhiteSpace(LocationId) ? "Tap to enter location ID" : LocationId;

    public string DeviceId
    {
        get => _deviceId;
        set
        {
            if (SetProperty(ref _deviceId, value))
            {
                OnPropertyChanged(nameof(DeviceIdSummary));
            }
        }
    }

    public string DeviceIdSummary =>
        string.IsNullOrWhiteSpace(DeviceId) ? "Tap to enter device ID" : DeviceId;

    public bool IsSandbox
    {
        get => _isSandbox;
        set
        {
            if (SetProperty(ref _isSandbox, value))
            {
                Environment = value ? "sandbox" : "production";
            }
        }
    }

    public string Environment
    {
        get => _environment;
        private set => SetProperty(ref _environment, value);
    }

    public string BarTabCardCatalogVariationId
    {
        get => _barTabCardCatalogVariationId;
        set
        {
            if (SetProperty(ref _barTabCardCatalogVariationId, value))
            {
                OnPropertyChanged(nameof(BarTabVariationIdSummary));
            }
        }
    }

    public string BarTabVariationIdSummary =>
        string.IsNullOrWhiteSpace(BarTabCardCatalogVariationId) ? "Tap to enter variation ID" : BarTabCardCatalogVariationId;

    public string GuestTabCardCatalogVariationId
    {
        get => _guestTabCardCatalogVariationId;
        set
        {
            if (SetProperty(ref _guestTabCardCatalogVariationId, value))
            {
                OnPropertyChanged(nameof(GuestTabVariationIdSummary));
            }
        }
    }

    public string GuestTabVariationIdSummary =>
        string.IsNullOrWhiteSpace(GuestTabCardCatalogVariationId) ? "Tap to enter variation ID" : GuestTabCardCatalogVariationId;

    public double CardSurchargePercent
    {
        get => _cardSurchargePercent;
        set
        {
            if (SetProperty(ref _cardSurchargePercent, value))
            {
                OnPropertyChanged(nameof(CardSurchargePercentText));
            }
        }
    }

    public string CardSurchargePercentText =>
        _cardSurchargePercent.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    public IAsyncRelayCommand EditCardSurchargePercentCommand { get; }

    public IAsyncRelayCommand LoadCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand EditAccessTokenCommand { get; }

    public IAsyncRelayCommand EditLocationIdCommand { get; }

    public IAsyncRelayCommand EditDeviceIdCommand { get; }

    public IAsyncRelayCommand EditBarTabVariationIdCommand { get; }

    public IAsyncRelayCommand EditGuestTabVariationIdCommand { get; }

    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            var current = await _config.LoadAsync().ConfigureAwait(true);
            AccessToken = current.AccessToken;
            LocationId = current.LocationId;
            DeviceId = current.DeviceId;
            IsSandbox = string.Equals(current.Environment, "sandbox", StringComparison.OrdinalIgnoreCase);
            BarTabCardCatalogVariationId = current.BarTabCardCatalogVariationId;
            GuestTabCardCatalogVariationId = current.GuestTabCardCatalogVariationId;
            CardSurchargePercent = current.PitstopTerminalCardSurchargePercent > 0
                ? (double)decimal.Round(current.PitstopTerminalCardSurchargePercent, 2, MidpointRounding.AwayFromZero)
                : 1.7;
            SetStatus("Loaded.");
        }
        catch (Exception ex)
        {
            SetStatus($"Load failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            IsBusy = true;
            NotifyBusyChanged();
            await _config.SaveAsync(new AppSquareConfig
            {
                AccessToken = (AccessToken ?? string.Empty).Trim(),
                LocationId = (LocationId ?? string.Empty).Trim(),
                DeviceId = (DeviceId ?? string.Empty).Trim(),
                Environment = IsSandbox ? "sandbox" : "production",
                BarTabCardCatalogVariationId = (BarTabCardCatalogVariationId ?? string.Empty).Trim(),
                GuestTabCardCatalogVariationId = (GuestTabCardCatalogVariationId ?? string.Empty).Trim(),
                PitstopTerminalCardSurchargePercent = (decimal)Math.Clamp(CardSurchargePercent, 0d, 100d),
            }).ConfigureAwait(true);
            SetStatus("Saved.");
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            NotifyBusyChanged();
        }
    }

    private async Task EditAccessTokenAsync()
    {
        var result = await _inputOverlay.ShowKeyboardAsync(AccessToken ?? string.Empty, "Square access token").ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        AccessToken = result.Trim();
    }

    private async Task EditLocationIdAsync()
    {
        var result = await _inputOverlay.ShowKeyboardAsync(LocationId ?? string.Empty, "Square location ID").ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        LocationId = result.Trim();
    }

    private async Task EditDeviceIdAsync()
    {
        var result = await _inputOverlay.ShowKeyboardAsync(DeviceId ?? string.Empty, "Square device ID").ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        DeviceId = result.Trim();
    }

    private async Task EditBarTabVariationIdAsync()
    {
        var result = await _inputOverlay.ShowKeyboardAsync(BarTabCardCatalogVariationId ?? string.Empty, "Bar tab card catalog variation ID").ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        BarTabCardCatalogVariationId = result.Trim();
    }

    private async Task ImportFromV2Async()
    {
        try
        {
            IsBusy = true;
            NotifyBusyChanged();

            var detected = _import.TryFindLegacyConfigPath();
            var path = await _filePicker.PickSquareConfigFileAsync(detected).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(path))
            {
                SetStatus("Import cancelled.");
                return;
            }

            var result = await _import.ImportFromPathAsync(path, overwriteExisting: true).ConfigureAwait(true);
            if (result.Imported)
            {
                SetStatus(result.Message ?? "Imported.");
                await LoadAsync().ConfigureAwait(true);
            }
            else
            {
                SetStatus(result.Message ?? "Import skipped.");
            }

            RefreshLegacyPathProbe();
        }
        catch (Exception ex)
        {
            SetStatus($"Import failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            NotifyBusyChanged();
        }
    }

    private void RefreshLegacyPathProbe()
    {
        LegacyConfigPath = _import.TryFindLegacyConfigPath() ?? string.Empty;
    }

    private async Task EditGuestTabVariationIdAsync()
    {
        var result = await _inputOverlay.ShowKeyboardAsync(GuestTabCardCatalogVariationId ?? string.Empty, "Guest tab card catalog variation ID").ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        GuestTabCardCatalogVariationId = result.Trim();
    }

    private async Task EditCardSurchargePercentAsync()
    {
        var init = decimal.Round((decimal)CardSurchargePercent, 2, MidpointRounding.AwayFromZero);
        var result = await _inputOverlay.ShowNumpadAsync(init, "Card surcharge %", false, default).ConfigureAwait(true);
        if (result.HasValue)
        {
            var v = (double)decimal.Round(result.Value, 2, MidpointRounding.AwayFromZero);
            CardSurchargePercent = Math.Clamp(v, 0d, 100d);
        }
    }

    private void NotifyBusyChanged()
    {
        SaveCommand.NotifyCanExecuteChanged();
        ImportFromV2Command.NotifyCanExecuteChanged();
        EditAccessTokenCommand.NotifyCanExecuteChanged();
        EditLocationIdCommand.NotifyCanExecuteChanged();
        EditDeviceIdCommand.NotifyCanExecuteChanged();
        EditBarTabVariationIdCommand.NotifyCanExecuteChanged();
        EditGuestTabVariationIdCommand.NotifyCanExecuteChanged();
        EditCardSurchargePercentCommand.NotifyCanExecuteChanged();
    }
}
