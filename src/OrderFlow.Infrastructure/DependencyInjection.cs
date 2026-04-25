using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Abstractions.Idempotency;
using OrderFlow.Application.Abstractions.Notifications;
using OrderFlow.Application.Abstractions.Payments;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common.Resilience;
using OrderFlow.Infrastructure.Idempotency;
using OrderFlow.Infrastructure.Outbox;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Infrastructure.Persistence.Interceptors;
using OrderFlow.Infrastructure.Repositories;
using OrderFlow.Infrastructure.Services;
using Polly;
using Polly.Retry;

namespace OrderFlow.Infrastructure;

/// <summary>DI registration for the infrastructure layer.</summary>
public static class DependencyInjection
{
    /// <summary>Connection-string key looked up in <see cref="IConfiguration"/>.</summary>
    public const string ConnectionStringName = "Postgres";

    /// <summary>
    /// Registers the EF Core <see cref="AppDbContext"/> (Npgsql), repositories,
    /// unit of work, outbox interceptor, the <see cref="OutboxPublisherService"/>,
    /// simulated payment/notification services, and the Polly pipeline used by
    /// the domain event handlers.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<OutboxMessageInterceptor>();
        services.AddSingleton<RowVersionInterceptor>();

        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' was not configured.");

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(2),
                    errorCodesToAdd: null);
            });
            options.AddInterceptors(
                sp.GetRequiredService<RowVersionInterceptor>(),
                sp.GetRequiredService<OutboxMessageInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();

        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddHostedService<OutboxPublisherService>();

        services.AddScoped<IPaymentGateway, LoggingPaymentGateway>();
        services.AddScoped<IEmailNotifier, LoggingEmailNotifier>();

        services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();
        services.AddHostedService<IdempotencyCleanupService>();

        services.AddResiliencePipeline(ResiliencePipelines.EventHandler, pipeline =>
        {
            pipeline.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(200),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex =>
                    ex is not OperationCanceledException)
            });
        });

        return services;
    }
}
