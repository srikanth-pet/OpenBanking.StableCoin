using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenBanking.StableCoin.Application.Interfaces.ExternalClients;
using OpenBanking.StableCoin.Application.Interfaces.Repositories;
using OpenBanking.StableCoin.Infrastructure.Coinbase.Auth;
using OpenBanking.StableCoin.Infrastructure.Coinbase.Clients;
using OpenBanking.StableCoin.Infrastructure.Coinbase.Resilience;
using OpenBanking.StableCoin.Infrastructure.Coinbase.Webhook;
using OpenBanking.StableCoin.Infrastructure.Configuration;
using OpenBanking.StableCoin.Infrastructure.Persistence;
using OpenBanking.StableCoin.Infrastructure.Persistence.Repositories;

namespace OpenBanking.StableCoin.Infrastructure.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuration
        services.Configure<CoinbaseOptions>(configuration.GetSection(CoinbaseOptions.SectionKey));

        // Database
        services.AddDbContext<StablecoinDbContext>(opts =>
            opts.UseNpgsql(
                configuration.GetConnectionString("StablecoinDb"),
                npgsql => npgsql.MigrationsHistoryTable(
                    "__ef_migrations_history", "stablecoin")));

        // JWT generator — singleton (stateless, thread-safe, no per-request state)
        services.AddSingleton<ICoinbaseJwtTokenGenerator, CoinbaseJwtTokenGenerator>();

        // Auth delegating handler — transient (new instance per HttpClient pipeline)
        services.AddTransient<CoinbaseAuthHandler>();

        // Advanced Trade HTTP Client
        services.AddHttpClient<ICoinbaseAdvancedTradeClient, CoinbaseAdvancedTradeClient>(client =>
            {
                client.BaseAddress = new Uri(
                    configuration["Coinbase:AdvancedTradeBaseUrl"]
                    ?? "https://api.coinbase.com/api/v3/brokerage/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "OpenBanking-StableCoin/1.0");
            })
            .AddHttpMessageHandler<CoinbaseAuthHandler>()
            .AddStandardResilienceHandler(CoinbaseResiliencePolicies.Configure());

        // CDP Wallet HTTP Client
        services.AddHttpClient<ICoinbaseCDPWalletClient, CoinbaseCDPWalletClient>(client =>
            {
                client.BaseAddress = new Uri(
                    configuration["Coinbase:CdpWalletBaseUrl"]
                    ?? "https://api.cdp.coinbase.com/platform/v1/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "OpenBanking-StableCoin/1.0");
            })
            .AddHttpMessageHandler<CoinbaseAuthHandler>()
            .AddStandardResilienceHandler(CoinbaseResiliencePolicies.Configure());

        // Repositories
        services.AddScoped<IStablecoinOrderRepository, StablecoinOrderRepository>();
        services.AddScoped<ICustomerWalletRepository, CustomerWalletRepository>();
        services.AddScoped<IWalletTransferRepository, WalletTransferRepository>();

        // Webhook validator — singleton (stateless)
        services.AddSingleton<ICoinbaseWebhookValidator, CoinbaseWebhookValidator>();

        return services;
    }
}
