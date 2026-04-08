using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OpenBanking.StableCoin.Application.Interfaces.Services;
using OpenBanking.StableCoin.Application.Services;

namespace OpenBanking.StableCoin.Application.DependencyInjection;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IStablecoinTradingService, StablecoinTradingService>();
        services.AddScoped<IStablecoinWalletService, StablecoinWalletService>();

        // Register all FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(
            typeof(ApplicationServiceExtensions).Assembly,
            ServiceLifetime.Scoped);

        return services;
    }
}
