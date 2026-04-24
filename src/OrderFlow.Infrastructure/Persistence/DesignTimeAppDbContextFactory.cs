using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderFlow.Infrastructure.Persistence;

/// <summary>
/// Used by the <c>dotnet ef</c> tooling to create an <see cref="AppDbContext"/>
/// outside of the ASP.NET Core host. Only the connection string is needed here —
/// the interceptor registration and hosted services live in the API composition root.
/// </summary>
/// <remarks>
/// The tooling uses the <c>ConnectionStrings__Postgres</c> environment variable if
/// set; otherwise it falls back to the local-dev default matching docker-compose.
/// </remarks>
public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=orderflow;Username=orderflow;Password=orderflow";

    /// <inheritdoc />
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
