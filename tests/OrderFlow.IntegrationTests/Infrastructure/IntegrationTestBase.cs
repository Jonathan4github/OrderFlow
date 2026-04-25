using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.IntegrationTests.Infrastructure;

/// <summary>
/// Shared base for HTTP-level integration tests. Each test class brings up a
/// dedicated Postgres container (via <see cref="PostgresContainerFixture"/>)
/// and a fresh <see cref="OrderFlowApplicationFactory"/>. Tests reset DB
/// state explicitly so order-of-execution does not leak rows between cases.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private OrderFlowApplicationFactory? _factory;
    private HttpClient? _client;

    /// <summary>Postgres fixture — injected via <c>IClassFixture&lt;PostgresContainerFixture&gt;</c>.</summary>
    protected PostgresContainerFixture Postgres { get; }

    /// <summary>HTTP client wired against the in-process API.</summary>
    protected HttpClient Client => _client ?? throw new InvalidOperationException("Test not initialised.");

    /// <summary>Service provider rooted at the WebApplication host.</summary>
    protected IServiceProvider Services =>
        _factory?.Services ?? throw new InvalidOperationException("Test not initialised.");

    /// <summary>Creates a new instance bound to the supplied container fixture.</summary>
    protected IntegrationTestBase(PostgresContainerFixture postgres)
    {
        Postgres = postgres;
    }

    /// <inheritdoc />
    public virtual Task InitializeAsync()
    {
        _factory = new OrderFlowApplicationFactory(Postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    /// <summary>Truncates state tables and seeds the given products + inventory.</summary>
    protected Task ResetWithSeedAsync(
        params (Guid Id, string Name, decimal Price, int Stock)[] seed) =>
        Postgres.ResetAsync(Services, seed);

    /// <summary>Helper to read a JSON body into a typed result.</summary>
    protected static Task<T?> ReadJsonAsync<T>(HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<T>();

    /// <summary>Opens a fresh DB scope so tests can inspect post-call state.</summary>
    protected AsyncServiceScope CreateDbScope() => Services.CreateAsyncScope();

    /// <summary>Convenience: get an <see cref="AppDbContext"/> from a scope.</summary>
    protected static AppDbContext Db(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<AppDbContext>();
}
