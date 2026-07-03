using Microsoft.Extensions.DependencyInjection;
using NickeltownPOSV4.Data.Migration;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.Migration;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.DependencyInjection;

internal static class ServiceCollectionMigrationExtensions
{
    public static IServiceCollection AddMigrationServices(this IServiceCollection services)
    {
        services.AddSingleton<IMigrationFingerprintStore, SqliteMigrationFingerprintStore>();
        services.AddSingleton<IMigrationRunJournal, SqliteMigrationRunJournal>();
        services.AddSingleton<IMemberMigrationRepository, SqliteMemberRepository>();
        services.AddSingleton<IItemMigrationRepository, SqliteItemRepository>();
        services.AddSingleton<IDrinkMigrationRepository, SqliteDrinkRepository>();
        services.AddSingleton<ICategoryMigrationRepository, SqliteCategoryRepository>();
        services.AddSingleton<IBartenderMigrationRepository, SqliteBartenderRepository>();
        services.AddSingleton<IPitstopSalesMigrationRepository, SqlitePitstopSalesRepository>();
        services.AddSingleton<ISquareConfigMigrationRepository, SqliteSquareConfigRepository>();
        services.AddSingleton<IAppSettingsMigrationRepository, SqliteAppSettingsMigrationRepository>();
        services.AddSingleton<ILegacyJsonFileDetector, LegacyJsonFileDetector>();
        services.AddSingleton<IMigrationBackupService, MigrationBackupService>();
        services.AddSingleton<IJsonImportService, JsonImportService>();
        services.AddSingleton<IImportDatabaseStatistics, SqliteImportStatisticsReader>();
        services.AddSingleton<IMigrationFolderPicker, WinUIMigrationFolderPicker>();
        services.AddSingleton<MigrationWizardViewModel>();

        return services;
    }
}
