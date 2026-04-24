using System.Reflection;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using OrderFlow.API.Middleware;
using OrderFlow.API.Seeding;
using OrderFlow.Application;
using OrderFlow.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting OrderFlow.API host");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "OrderFlow.API"));

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "OrderFlow API",
            Version = "v1",
            Description = "Resilient order-processing API."
        });

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    });

    builder.Services.AddMemoryCache();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var healthChecksBuilder = builder.Services.AddHealthChecks();
    var connectionString = builder.Configuration.GetConnectionString("Postgres");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        healthChecksBuilder.AddNpgSql(connectionString, name: "postgres", tags: ["db", "ready"]);
    }

    var app = builder.Build();

    // Pipeline order is deliberate:
    //   Serilog request logging → GlobalExceptionHandler → Idempotency → routing/endpoints.
    // Putting the exception handler before idempotency means a cached response is
    // only written when the downstream pipeline produced one cleanly.
    app.UseSerilogRequestLogging();
    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    app.UseMiddleware<IdempotencyMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "OrderFlow API v1");
            options.DocumentTitle = "OrderFlow API";
        });
    }

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.UseAuthorization();
    app.MapControllers();

    // Apply migrations + seed demo data at startup.
    // Skipped under the IntegrationTests environment so tests can
    // provision and seed their own Testcontainers-backed database.
    if (!app.Environment.IsEnvironment("IntegrationTests"))
    {
        await DatabaseSeeder.MigrateAndSeedAsync(app.Services);
    }

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "OrderFlow.API terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

namespace OrderFlow.API
{
    /// <summary>Program entry point. Exposed for integration testing via WebApplicationFactory.</summary>
    public partial class Program;
}
