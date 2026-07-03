using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.ViewModels.Settings;

public sealed class ComPortConfigViewModel : SettingsSubViewModelBase
{
    private readonly IComPortConfigService _config;
    private readonly ISerialCashDrawerService _drawer;

    private string? _selectedPort;
    private int _selectedBaudRate = 9600;

    public ComPortConfigViewModel(
        INavigationService navigation,
        IComPortConfigService config,
        ISerialCashDrawerService drawer)
        : base(navigation)
    {
        _config = config;
        _drawer = drawer;

        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        KickDrawerCommand = new AsyncRelayCommand(KickDrawerAsync, () => !IsBusy);
        LoadCommand = new AsyncRelayCommand(LoadAsync);

        foreach (var baud in _config.GetCommonBaudRates())
        {
            BaudRates.Add(baud);
        }

        RefreshPorts();
    }

    public ObservableCollection<string> AvailablePorts { get; } = new();

    public ObservableCollection<int> BaudRates { get; } = new();

    public string? SelectedPort
    {
        get => _selectedPort;
        set => SetProperty(ref _selectedPort, value);
    }

    public int SelectedBaudRate
    {
        get => _selectedBaudRate;
        set => SetProperty(ref _selectedBaudRate, value);
    }

    public IRelayCommand RefreshPortsCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand KickDrawerCommand { get; }

    public IAsyncRelayCommand LoadCommand { get; }

    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var p in _config.ListAvailablePorts())
        {
            AvailablePorts.Add(p);
        }

        if (!string.IsNullOrEmpty(SelectedPort) && !AvailablePorts.Contains(SelectedPort))
        {
            AvailablePorts.Add(SelectedPort);
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            var current = await _config.LoadAsync().ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(current.PortName) && !AvailablePorts.Contains(current.PortName))
            {
                AvailablePorts.Add(current.PortName);
            }

            SelectedPort = string.IsNullOrWhiteSpace(current.PortName) ? AvailablePorts.Count > 0 ? AvailablePorts[0] : null : current.PortName;
            SelectedBaudRate = BaudRates.Contains(current.BaudRate) ? current.BaudRate : 9600;
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
        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            SetStatus("Pick a COM port before saving.");
            return;
        }

        try
        {
            IsBusy = true;
            SaveCommand.NotifyCanExecuteChanged();
            KickDrawerCommand.NotifyCanExecuteChanged();

            await _config.SaveAsync(new AppComPortConfig
            {
                PortName = SelectedPort,
                BaudRate = SelectedBaudRate,
            }).ConfigureAwait(true);

            SetStatus($"Saved {SelectedPort} @ {SelectedBaudRate} baud.");
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            SaveCommand.NotifyCanExecuteChanged();
            KickDrawerCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task KickDrawerAsync()
    {
        try
        {
            IsBusy = true;
            SaveCommand.NotifyCanExecuteChanged();
            KickDrawerCommand.NotifyCanExecuteChanged();
            SetStatus("Sending drawer-kick pulse...");
            await _drawer.KickAsync().ConfigureAwait(true);
            SetStatus("Drawer-kick pulse sent.");
        }
        catch (Exception ex)
        {
            SetStatus($"Drawer kick failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            SaveCommand.NotifyCanExecuteChanged();
            KickDrawerCommand.NotifyCanExecuteChanged();
        }
    }
}
