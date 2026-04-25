using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderFlow.IntegrationTests.Infrastructure;

/// <summary>
/// Minimal RFC-7807 ProblemDetails shape with overflow extension data so tests
/// can assert on the API's domain-specific extras (productId, requested, etc.).
/// </summary>
public sealed class ProblemDetailsExtras
{
    [JsonPropertyName("status")] public int Status { get; set; }
    [JsonPropertyName("title")]  public string? Title { get; set; }
    [JsonPropertyName("detail")] public string? Detail { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extensions { get; set; } = new();
}
