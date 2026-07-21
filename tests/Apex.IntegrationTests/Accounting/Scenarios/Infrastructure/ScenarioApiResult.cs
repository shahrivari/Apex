using System.Net;

namespace Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;

/// <summary>
/// Typed outcome of one <see cref="ScenarioApiClient"/> call. Always carries the raw response
/// body alongside the decoded value/problem so assertion failures can show exactly what the API
/// returned (spec §6.1: "Preserve useful response content in assertion failures").
/// </summary>
public sealed record ScenarioApiResult<T>(
    HttpStatusCode StatusCode, string? ContentType, string RawBody, T? Value, ProblemDetailsPayload? Problem)
{
    public bool IsSuccess => (int)StatusCode is >= 200 and < 300;
}
