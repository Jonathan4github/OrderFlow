using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Common.Behaviors;

namespace OrderFlow.Application;

/// <summary>DI registration for the application layer.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR (with the validation pipeline behavior) and all
    /// FluentValidation validators discovered in this assembly.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
