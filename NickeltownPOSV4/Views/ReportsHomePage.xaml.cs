using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

/// <summary>Opened from the bottom-nav Reports tab. Entry point for PDF/CSV exports.</summary>
public sealed partial class ReportsHomePage : Page
{
    public ReportsHomePage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ReportsHomeViewModel>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    }
}
