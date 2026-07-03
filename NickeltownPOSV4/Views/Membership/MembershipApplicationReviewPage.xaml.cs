using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels.Membership;

namespace NickeltownPOSV4.Views.Membership;

public sealed partial class MembershipApplicationReviewPage : Page
{
    private MembershipApplicationReviewViewModel? _viewModel;

    public MembershipApplicationReviewPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<MembershipApplicationReviewViewModel>();
        DataContext = _viewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (_viewModel is null)
        {
            return;
        }

        var applicationId = e.Parameter is long id ? id : 0L;
        if (applicationId > 0)
        {
            await _viewModel.LoadAsync(applicationId);
        }
    }
}