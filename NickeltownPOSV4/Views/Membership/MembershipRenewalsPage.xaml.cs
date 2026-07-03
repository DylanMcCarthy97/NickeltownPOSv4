using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels.Membership;

namespace NickeltownPOSV4.Views.Membership;

public sealed partial class MembershipRenewalsPage : Page
{
    public MembershipRenewalsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MembershipRenewalsViewModel>();
    }
}
