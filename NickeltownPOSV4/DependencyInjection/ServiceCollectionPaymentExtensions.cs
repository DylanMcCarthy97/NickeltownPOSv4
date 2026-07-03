using Microsoft.Extensions.DependencyInjection;
using NickeltownPOSV4.Services.Payments;

namespace NickeltownPOSV4.DependencyInjection;

internal static class ServiceCollectionPaymentExtensions
{
    public static IServiceCollection AddPaymentServices(this IServiceCollection services)
    {
        services.AddSingleton<ISquareCardPaymentOrchestrator, SquareCardPaymentOrchestrator>();
        return services;
    }
}