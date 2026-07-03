using Microsoft.Extensions.DependencyInjection;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.Stock;
using NickeltownPOSV4.ViewModels;
using NickeltownPOSV4.Views.Panels;

namespace NickeltownPOSV4.DependencyInjection;

internal static class ServiceCollectionStockExtensions
{
    public static IServiceCollection AddStockServices(this IServiceCollection services)
    {
        services.AddSingleton<IStockEditingService, SqliteStockEditingService>();
        services.AddSingleton<IStockProductImageStorage, StockProductImageStorage>();
        services.AddSingleton<StockItemAdminPersistenceService>();
        services.AddSingleton<StockV2ItemImportPreviewService>();
        services.AddTransient<StockEditorPanelViewModel>();
        services.AddTransient<StockEditorPanel>();
        services.AddTransient<StockManagementPageViewModel>();

        return services;
    }
}
