using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels;
using Windows.System;

namespace NickeltownPOSV4.Views;

public sealed partial class LoginPage : Page
{
    public LoginPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Disabled;
        DataContext = App.Services.GetRequiredService<LoginViewModel>();
        KeyDown += OnLoginKeyDown;
    }

    private void OnLoginKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (DataContext is not LoginViewModel vm)
        {
            return;
        }

        if (e.Key == VirtualKey.Back)
        {
            if (vm.NumpadBackspaceCommand.CanExecute(null))
            {
                vm.NumpadBackspaceCommand.Execute(null);
            }

            e.Handled = true;
            return;
        }

        if (TryMapDigit(e.Key, out var digit))
        {
            if (vm.NumpadDigitCommand.CanExecute(digit))
            {
                vm.NumpadDigitCommand.Execute(digit);
            }

            e.Handled = true;
        }
    }

    private static bool TryMapDigit(VirtualKey key, out string digit)
    {
        if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
        {
            digit = ((char)('0' + (key - VirtualKey.Number0))).ToString();
            return true;
        }

        if (key >= VirtualKey.NumberPad0 && key <= VirtualKey.NumberPad9)
        {
            digit = ((char)('0' + (key - VirtualKey.NumberPad0))).ToString();
            return true;
        }

        digit = string.Empty;
        return false;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (DataContext is LoginViewModel vm)
        {
            vm.ResetForDisplay();
        }

        Focus(FocusState.Programmatic);
    }

    private void CloseApp_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Exit();
    }
}
