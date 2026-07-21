using System.Text.Json.Serialization;

namespace Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;

/// <summary>
/// Shape of the <c>application/problem+json</c> body written by
/// <c>GlobalExceptionHandlingMiddleware</c>: <c>errorCode</c> and <c>traceId</c> are always
/// present; <c>errors</c> is populated only for FluentValidation failures (400).
/// </summary>
public sealed class ProblemDetailsPayload
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("status")]
    public int? Status { get; init; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    [JsonPropertyName("errors")]
    public Dictionary<string, string[]>? Errors { get; init; }
}
