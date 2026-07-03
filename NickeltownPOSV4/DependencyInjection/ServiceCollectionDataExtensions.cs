using Microsoft.Extensions.DependencyInjection;
using NickeltownPOSV4.Data;
using NickeltownPOSV4.Data.Migration;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.DependencyInjection;

internal static class ServiceCollectionDataExtensions
{
    public static IServiceCollection AddDataServices(this IServiceCollection services)
    {
        services.AddSingleton<IAppStoragePaths, AppStoragePaths>();
        services.AddSingleton<IAppStorageMigrationService, AppStorageMigrationService>();
        services.AddSingleton<IUnitOfWork, NullUnitOfWork>();
        services.AddSingleton<SqliteConnectionFactory>(sp =>
            new SqliteConnectionFactory(sp.GetRequiredService<IAppStoragePaths>().DatabasePath));
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<AppDatabase>();
        services.AddSingleton<SqliteTabRepository>();
        services.AddSingleton<ITabMigrationRepository>(sp => sp.GetRequiredService<SqliteTabRepository>());
        services.AddSingleton<ITabWorkspaceQuery>(sp => sp.GetRequiredService<SqliteTabRepository>());
        services.AddSingleton<IMemberDirectoryQuery, SqliteMemberDirectoryQuery>();
        services.AddSingleton<SqliteBarCatalogQuery>();
        services.AddSingleton<IBarCatalogCache, BarCatalogCache>();
        services.AddSingleton<IItemCatalogQuery, CachingBarCatalogQuery>();
        services.AddSingleton<IInsideBarSalesSummaryQuery, SqliteInsideBarSalesSummaryQuery>();
        services.AddSingleton<ISquarePaymentAttemptRepository, SqliteSquarePaymentAttemptRepository>();
        services.AddSingleton<ISquareRecoveryRepository, SqliteSquareRecoveryRepository>();
        services.AddSingleton<IAuditLogRepository, SqliteAuditLogRepository>();
        services.AddSingleton<IAuditLogService, AuditLogService>();
        services.AddSingleton<GoogleDriveBackupUploader>();
        services.AddSingleton<ScheduledMaintenanceService>();
        services.AddSingleton<PosCatalogAutoRefreshService>();

        return services;
    }
}
