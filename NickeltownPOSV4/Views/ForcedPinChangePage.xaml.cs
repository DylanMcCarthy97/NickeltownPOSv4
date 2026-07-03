using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class ForcedPinChangePage : Page
{
    public ForcedPinChangePage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ForcedPinChangeViewModel>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (DataContext is ForcedPinChangeViewModel vm)
        {
            vm.ResetForDisplay();
        }
    }
}
