using Microsoft.Extensions.DependencyInjection;
using NickeltownPOSV4.Services.AddDrinks;

namespace NickeltownPOSV4.DependencyInjection;

internal static class ServiceCollectionAddDrinksExtensions
{
    public static IServiceCollection AddAddDrinksServices(this IServiceCollection services)
    {
        services.AddSingleton<AddDrinksSaleCommitService>();
        services.AddSingleton<IShotMixerConfigService, ShotMixerConfigService>();
        services.AddSingleton<IShotMixerBootstrapper, ShotMixerBootstrapper>();
        return services;
    }
}
