using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderFlow.Infrastructure.Outbox;

namespace OrderFlow.IntegrationTests.Infrastructure;

/// <summary>
/// Test factory that boots the full <c>OrderFlow.API</c> host wired against the
/// Testcontainers-managed Postgres. The <c>IntegrationTests</c> environment
/// suppresses the default seeder so each test class controls its own seed
/// state via <see cref="PostgresContainerFixture.ResetAsync"/>.
/// </summary>
public sealed class OrderFlowApplicationFactory : WebApplicationFactory<OrderFlow.API.Program>
{
    private readonly string _connectionString;

    /// <summary>Constructs the factory and pins the connection string via env vars.</summary>
    /// <remarks>
    /// In .NET 8 minimal hosting, <c>WebApplicationFactory.ConfigureWebHost</c>
    /// hooks the underlying <see cref="IHostBuilder"/>, but
    /// <c>builder.Configuration</c> (used in user code in <c>Program.cs</c>)
    /// is already finalised by the time those hooks fire. Setting the
    /// connection string as a process environment variable guarantees it is
    /// visible to <see cref="ConfigurationManager"/> the moment
    /// <c>WebApplication.CreateBuilder</c> wires up its providers.
    /// </remarks>
    public OrderFlowApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", connectionString);
        Environment.SetEnvironmentVariable("OrderFlow__Outbox__PollingIntervalSeconds", "1");
    }

    /// <inheritdoc />
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _connectionString,
                [$"{OutboxOptions.SectionName}:PollingIntervalSeconds"] = "1",
                [$"{OutboxOptions.SectionName}:BatchSize"] = "20"
            });
        });
        return base.CreateHost(builder);
    }
}
