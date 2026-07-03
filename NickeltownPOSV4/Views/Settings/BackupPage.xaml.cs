using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels.Settings;

namespace NickeltownPOSV4.Views.Settings;

public sealed partial class BackupPage : Page
{
    public BackupPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<BackupViewModel>();
    }
}
