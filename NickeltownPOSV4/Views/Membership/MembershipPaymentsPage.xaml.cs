using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels.Membership;

namespace NickeltownPOSV4.Views.Membership;

public sealed partial class MembershipPaymentsPage : Page
{
    public MembershipPaymentsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MembershipPaymentsViewModel>();
    }
}
