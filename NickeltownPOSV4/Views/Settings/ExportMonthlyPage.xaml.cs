using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels.Settings;

namespace NickeltownPOSV4.Views.Settings;

public sealed partial class ExportMonthlyPage : Page
{
    public ExportMonthlyPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ExportMonthlyViewModel>();
    }
}
