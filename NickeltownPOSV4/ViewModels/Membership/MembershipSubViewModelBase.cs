using System;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.ViewModels;
using NickeltownPOSV4.Views.Membership;

namespace NickeltownPOSV4.ViewModels.Membership;

public abstract class MembershipSubViewModelBase : ObservableViewModel
{
    private readonly INavigationService _navigation;

    private string _statusMessage = string.Empty;

    private bool _isBusy;

    protected MembershipSubViewModelBase(INavigationService navigation)
    {
        _navigation = navigation;
        BackToMembershipHomeCommand = new RelayCommand(BackToMembershipHome);
    }

    public IRelayCommand BackToMembershipHomeCommand { get; }

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

    protected void Navigate(Type pageType, object? parameter = null) =>
        _navigation.Navigate(pageType, parameter);

    private void BackToMembershipHome() => _navigation.Navigate(typeof(MembershipHomePage));
}
