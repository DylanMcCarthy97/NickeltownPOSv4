using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class PitstopEndOfDayReportPage : Page
{
    public PitstopEndOfDayReportPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<PitstopEndOfDayReportViewModel>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (DataContext is PitstopEndOfDayReportViewModel vm)
        {
            await vm.InitializeAsync().ConfigureAwait(true);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        App.Services.GetRequiredService<INavigationService>().TryGoBack();
    }

    private void JumpSection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string section)
        {
            return;
        }

        FrameworkElement? target = section switch
        {
            "Event" => EodSectionEvent,
            "Pos" => EodSectionPos,
            "Square" => EodSectionSquare,
            "Outside" => EodSectionOutsideTerminalSales,
            "Prizes" => EodSectionPrizes,
            "Costs" => EodSectionCosts,
            "Summary" => EodSectionSummary,
            _ => null,
        };

        target?.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true });
    }
}
