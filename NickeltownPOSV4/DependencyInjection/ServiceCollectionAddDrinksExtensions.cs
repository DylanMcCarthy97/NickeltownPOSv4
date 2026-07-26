using Microsoft.Extensions.DependencyInjection;
using NickeltownPOSV4.Services.AddDrinks;
using NickeltownPOSV4.Services.CustomAmount;

namespace NickeltownPOSV4.DependencyInjection;

internal static class ServiceCollectionAddDrinksExtensions
{
    public static IServiceCollection AddAddDrinksServices(this IServiceCollection services)
    {
        services.AddSingleton<AddDrinksSaleCommitService>();
        services.AddSingleton<IShotMixerConfigService, ShotMixerConfigService>();
        services.AddSingleton<IShotMixerBootstrapper, ShotMixerBootstrapper>();
        services.AddSingleton<ICustomAmountBootstrapper, CustomAmountBootstrapper>();
        return services;
    }
}
