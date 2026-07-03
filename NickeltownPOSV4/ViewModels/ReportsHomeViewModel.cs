using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Views;
using NickeltownPOSV4.Views.Settings;

namespace NickeltownPOSV4.ViewModels;

/// <summary>Hub for the bottom-nav Reports route: monthly tabs + stock exports live here.</summary>
public sealed class ReportsHomeViewModel
{
    private readonly INavigationService _navigation;

    public ReportsHomeViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        OpenExportsCommand = new RelayCommand(() => _navigation.Navigate(typeof(ExportMonthlyPage)));
        OpenPitstopEndOfDayCommand = new RelayCommand(() => _navigation.Navigate(typeof(PitstopEndOfDayReportPage)));
    }

    public IRelayCommand OpenExportsCommand { get; }

    public IRelayCommand OpenPitstopEndOfDayCommand { get; }
}
