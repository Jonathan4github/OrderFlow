using System.Diagnostics.CodeAnalysis;
using Polly;
using Polly.Registry;

namespace OrderFlow.UnitTests.Application.EventHandlers;

/// <summary>
/// Minimal <see cref="ResiliencePipelineProvider{TKey}"/> used by the handler
/// tests. Always returns a no-op (pass-through) pipeline so the tests assert
/// the handler's orchestration logic without timing-dependent retry delays.
/// </summary>
internal sealed class TestResiliencePipelineProvider : ResiliencePipelineProvider<string>
{
    public override bool TryGetPipeline(string key, [NotNullWhen(true)] out ResiliencePipeline? pipeline)
    {
        pipeline = ResiliencePipeline.Empty;
        return true;
    }

    public override bool TryGetPipeline<TResult>(string key, [NotNullWhen(true)] out ResiliencePipeline<TResult>? pipeline)
    {
        pipeline = ResiliencePipeline<TResult>.Empty;
        return true;
    }
}