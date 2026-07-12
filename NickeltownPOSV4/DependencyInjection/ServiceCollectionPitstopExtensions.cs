using Microsoft.Extensions.DependencyInjection;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Pitstop;
using NickeltownPOSV4.ViewModels;
using NickeltownPOSV4.Views.Panels;

namespace NickeltownPOSV4.DependencyInjection;

internal static class ServiceCollectionPitstopExtensions
{
    public static IServiceCollection AddPitstopServices(this IServiceCollection services)
    {
        services.AddSingleton<IPitstopCatalogQuery, SqlitePitstopCatalogQuery>();
        services.AddSingleton<IPitstopRetailSaleRepository, SqlitePitstopRetailSaleRepository>();
        services.AddSingleton<IPitstopEodBatchRepository, SqlitePitstopEodBatchRepository>();
        services.AddSingleton<IPitstopHeldSaleRepository, SqlitePitstopHeldSaleRepository>();
        services.AddSingleton<PitstopOutsideLineCatalogBuilder>();
        services.AddSingleton<PitstopReportService>();
        services.AddSingleton<SquareOutsideOrderEnrichment>();
        services.AddSingleton<ISquarePaymentReconciliationService, SquarePaymentReconciliationService>();
        services.AddSingleton<PitstopEodReconciliationService>();
        services.AddSingleton<PitstopSurchargeConfigLoader>();
        services.AddSingleton<IPitstopPaymentRecoveryService, PitstopPaymentRecoveryService>();
        services.AddSingleton<PitstopRetailViewModel>();
        services.AddSingleton<IPitstopRetailCartHost>(sp => sp.GetRequiredService<PitstopRetailViewModel>());
        services.AddSingleton<PitstopEndOfDayReportViewModel>();
        services.AddTransient<PreviousPitstopsViewModel>();
        services.AddTransient<PreviousPitstopDetailViewModel>();
        services.AddTransient<PreviousPitstopItemsViewModel>();
        services.AddTransient<PreviousPitstopTransactionsViewModel>();
        services.AddTransient<PitstopHeldSalesPanelViewModel>();
        services.AddTransient<PitstopHeldSalesPanel>();

        return services;
    }
}
