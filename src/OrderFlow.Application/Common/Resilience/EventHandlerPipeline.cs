namespace OrderFlow.Application.Common.Resilience;

/// <summary>
/// Names of Polly resilience pipelines shared across the application.
/// Register via <c>ResiliencePipelineRegistry</c> in the infrastructure layer.
/// </summary>
public static class ResiliencePipelines
{
    /// <summary>
    /// Pipeline used by the three order event handlers. Configured with
    /// three retry attempts and exponential back-off (see
    /// <c>DependencyInjection.AddInfrastructure</c>).
    /// </summary>
    public const string EventHandler = "orderflow-event-handler";
}
