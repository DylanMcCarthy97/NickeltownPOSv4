using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.ViewModels.Settings;

namespace NickeltownPOSV4.Views.Settings;

public sealed partial class UserManagementPage : Page
{
    private UserManagementViewModel? _viewModel;

    public UserManagementPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<UserManagementViewModel>();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (_viewModel is not null)
        {
            await _viewModel.RefreshAsync();
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (_viewModel is not null)
        {
            await _viewModel.RefreshAsync();
        }
    }

    private void OnEditUserClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not FrameworkElement fe || fe.Tag is not long id)
        {
            return;
        }

        var existing = _viewModel.FindStaffById(id);
        if (existing is null)
        {
            return;
        }

        _viewModel.BeginEdit(existing);
    }

    private void OnResetPinClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not FrameworkElement fe || fe.Tag is not long id)
        {
            return;
        }

        var existing = _viewModel.FindStaffById(id);
        if (existing is null)
        {
            return;
        }

        _viewModel.BeginResetPin(existing);
    }

    private async void OnDeactivateClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not FrameworkElement fe || fe.Tag is not long id)
        {
            return;
        }

        var existing = _viewModel.FindStaffById(id);
        if (existing is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Deactivate {existing.DisplayName}?",
            Content = new TextBlock
            {
                Text = "Deactivating preserves history but blocks future sign-ins. You can reactivate from Edit.",
                TextWrapping = TextWrapping.WrapWholeWords,
            },
            PrimaryButtonText = "Deactivate",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        PosContentDialogHelper.ApplyPosStyle(dialog);
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        await _viewModel.DeactivateAsync(id, existing.DisplayName);
    }
}
