using Microsoft.Extensions.DependencyInjection;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Services.Updates;
using NickeltownPOSV4.ViewModels.Settings;
using NickeltownPOSV4.Views.Settings;

namespace NickeltownPOSV4.DependencyInjection;

internal static class ServiceCollectionSettingsExtensions
{
    public static IServiceCollection AddSettingsServices(this IServiceCollection services)
    {
        services.AddSingleton<ISquareTerminalSession, SquareSdkTerminalSession>();
        services.AddSingleton<IAppSettingsRepository, SqliteAppSettingsRepository>();
        services.AddSingleton<IEmailConfigService, EmailConfigService>();
        services.AddSingleton<IEmailConfigImportService, EmailConfigImportService>();
        services.AddSingleton<IEmailConfigFilePicker, WinUIEmailConfigFilePicker>();
        services.AddSingleton<IComPortConfigService, ComPortConfigService>();
        services.AddSingleton<ISquareConfigService, SquareConfigService>();
        services.AddSingleton<ISquareConfigImportService, SquareConfigImportService>();
        services.AddSingleton<ISquareConfigFilePicker, WinUISquareConfigFilePicker>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<ISerialCashDrawerService, SerialCashDrawerService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IReportExportService, SqliteReportExportService>();
        services.AddSingleton<IStaffAdminService, SqliteStaffAdminService>();
        services.AddSingleton<IAppUpdateConfigService, AppUpdateConfigService>();
        services.AddSingleton<IAppUpdateService, AppUpdateService>();

        services.AddTransient<BackupViewModel>();
        services.AddTransient<ComPortConfigViewModel>();
        services.AddTransient<EmailConfigViewModel>();
        services.AddTransient<SquareConfigViewModel>();
        services.AddTransient<UserManagementViewModel>();
        services.AddTransient<ArchivedTabsViewModel>();
        services.AddTransient<ExportMonthlyViewModel>();
        services.AddTransient<UpdateConfigViewModel>();
        services.AddTransient<BackupPage>();
        services.AddTransient<ComPortConfigPage>();
        services.AddTransient<EmailConfigPage>();
        services.AddTransient<SquareConfigPage>();
        services.AddTransient<UserManagementPage>();
        services.AddTransient<ArchivedTabsPage>();
        services.AddTransient<ExportMonthlyPage>();
        services.AddTransient<UpdateConfigPage>();

        return services;
    }
}
