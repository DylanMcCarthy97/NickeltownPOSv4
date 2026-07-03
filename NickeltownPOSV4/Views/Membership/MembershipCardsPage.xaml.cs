using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels.Membership;

namespace NickeltownPOSV4.Views.Membership;

public sealed partial class MembershipCardsPage : Page
{
    public MembershipCardsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MembershipCardsViewModel>();
    }
}
