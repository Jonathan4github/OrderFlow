namespace OrderFlow.API.Configuration;

/// <summary>
/// Bound from the <c>OrderFlow:Observability</c> configuration section.
/// Disabled by default so docker-compose stays minimal; flip
/// <see cref="OpenTelemetryEnabled"/> to <c>true</c> to switch on tracing.
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "OrderFlow:Observability";

    /// <summary>Toggles OpenTelemetry instrumentation + exporter wiring.</summary>
    public bool OpenTelemetryEnabled { get; set; }

    /// <summary>
    /// OTLP endpoint (gRPC) to ship traces to. When unset or
    /// <see cref="OpenTelemetryEnabled"/> is false, traces fall back
    /// to the console exporter so engineers can still see them locally.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>Service name advertised via Resource.</summary>
    public string ServiceName { get; set; } = "OrderFlow.API";
}
