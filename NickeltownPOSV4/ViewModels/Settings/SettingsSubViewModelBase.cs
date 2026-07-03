using System;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Views;

namespace NickeltownPOSV4.ViewModels.Settings;

/// <summary>Shared back-to-settings command for all settings sub-page view models.</summary>
public abstract class SettingsSubViewModelBase : ObservableViewModel
{
    private readonly INavigationService _navigation;

    private string _statusMessage = string.Empty;

    private bool _isBusy;

    protected SettingsSubViewModelBase(INavigationService navigation)
    {
        _navigation = navigation;
        BackToSettingsCommand = new RelayCommand(BackToSettings);
    }

    public IRelayCommand BackToSettingsCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        protected set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        protected set => SetProperty(ref _isBusy, value);
    }

    protected void SetStatus(string message) => StatusMessage = message ?? string.Empty;

    private void BackToSettings()
    {
        _navigation.Navigate(typeof(SettingsPage));
    }

    /// <summary>Helper for sub-classes that need to navigate elsewhere from a tile/button.</summary>
    protected void Navigate(Type pageType, object? parameter = null) =>
        _navigation.Navigate(pageType, parameter);

    /// <summary>Pops the shell navigation stack when possible (e.g. Reports home → export page).</summary>
    protected bool TryNavigateBack() => _navigation.TryGoBack();

    protected static ShellRoute SettingsRoute() =>
        new() { Id = "settings", Title = "Settings", Glyph = "\uE713" };
}
