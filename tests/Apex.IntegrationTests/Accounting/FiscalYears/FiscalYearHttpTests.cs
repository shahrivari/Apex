using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Apex.IntegrationTests.Common;
using Apex.IntegrationTests.Http;
using Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;
using Apex.Modules.Accounting.FiscalYears.UseCases.CancelFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.CreateFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.UpdateFiscalYear;

namespace Apex.IntegrationTests.Accounting.FiscalYears;

[Collection(ApexHttpIntegrationTestCollection.Name)]
public sealed class FiscalYearHttpTests : IAsyncLifetime
{
    private const string BaseUrl = "/api/v1/accounting/fiscal-years";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
    };

    private readonly ApexWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public FiscalYearHttpTests(ApexWebApplicationFactory factory) => _factory = factory;

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task UnauthenticatedRequest_ShouldReturn401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl);
        request.Headers.Add("X-Test-Unauthenticated", "true");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndGet_ShouldReturnPublicContract()
    {
        await _factory.ResetAccountingDatabaseAsync();
        var bookId = await CreateBookAsync("FY-HTTP-CREATE", "fy-http-create");

        var createResponse = await _client.PostAsJsonAsync(BaseUrl,
            CreateRequest(bookId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateFiscalYearResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("DRAFT", created.Status);
        Assert.Equal(new DateOnly(2025, 12, 31), created.FinalizedThroughDate);
        Assert.Equal(new Uri($"{BaseUrl}/{created.Id}", UriKind.Relative), createResponse.Headers.Location);

        var getResponse = await _client.GetAsync($"{BaseUrl}/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidDateRange_ShouldReturnValidationProblem()
    {
        await _factory.ResetAccountingDatabaseAsync();
        var bookId = await CreateBookAsync("FY-HTTP-INVALID", "fy-http-invalid");

        var response = await _client.PostAsJsonAsync(BaseUrl,
            CreateRequest(bookId, new DateOnly(2026, 12, 31), new DateOnly(2026, 1, 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemAsync(response, "validation_failed");
    }

    [Fact]
    public async Task Create_OverlappingRange_ShouldReturnConflictProblem()
    {
        await _factory.ResetAccountingDatabaseAsync();
        var bookId = await CreateBookAsync("FY-HTTP-OVERLAP", "fy-http-overlap");
        await CreateFiscalYearAsync(bookId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        var response = await _client.PostAsJsonAsync(BaseUrl,
            CreateRequest(bookId, new DateOnly(2026, 12, 1), new DateOnly(2027, 11, 30)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, "fiscal_year_dates_overlap");
    }

    [Fact]
    public async Task ListAndResolve_ShouldBindQueryParameters()
    {
        await _factory.ResetAccountingDatabaseAsync();
        var bookId = await CreateBookAsync("FY-HTTP-QUERY", "fy-http-query");
        var fiscalYear = await CreateFiscalYearAsync(
            bookId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        var listResponse = await _client.GetAsync(
            $"{BaseUrl}?accountingBookId={bookId}&status=DRAFT&fromDate=2026-01-01&toDate=2026-12-31&page=1&pageSize=10");
        var resolveResponse = await _client.GetAsync(
            $"{BaseUrl}/resolve?accountingBookId={bookId}&accountingDate=2026-06-01&requiredStatus=DRAFT");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
        var resolved = await resolveResponse.Content.ReadFromJsonAsync<ResolveResponse>(JsonOptions);
        Assert.Equal(fiscalYear.Id, resolved?.Id);
    }

    [Fact]
    public async Task List_InvalidPagination_ShouldReturnValidationProblem()
    {
        var response = await _client.GetAsync($"{BaseUrl}?page=0&pageSize=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemAsync(response, "validation_failed");
    }

    [Fact]
    public async Task MissingFiscalYear_ShouldReturnNotFoundProblem()
    {
        var response = await _client.GetAsync($"{BaseUrl}/99999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, "fiscal_year_not_found");
    }

    [Fact]
    public async Task UpdateAndDeleteDraft_ShouldReturnExpectedStatuses()
    {
        await _factory.ResetAccountingDatabaseAsync();
        var bookId = await CreateBookAsync("FY-HTTP-EDIT", "fy-http-edit");
        var fiscalYear = await CreateFiscalYearAsync(
            bookId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        var updateResponse = await _client.PutAsJsonAsync($"{BaseUrl}/{fiscalYear.Id}",
            new UpdateFiscalYearRequest
            {
                Title = "Updated fiscal year",
                StartDate = new DateOnly(2026, 2, 1),
                EndDate = new DateOnly(2027, 1, 31)
            });
        var deleteResponse = await _client.DeleteAsync($"{BaseUrl}/{fiscalYear.Id}");

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task OpenFinalizeAndCancel_ShouldReturnExpectedLifecycleContracts()
    {
        await _factory.ResetAccountingDatabaseAsync();
        var bookId = await CreateBookAsync("FY-HTTP-LIFECYCLE", "fy-http-lifecycle");
        var fiscalYear = await CreateFiscalYearAsync(
            bookId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var cancellationDate = new DateOnly(2026, 6, 30);

        var openResponse = await _client.PostAsync($"{BaseUrl}/{fiscalYear.Id}/open", null);
        var finalizeResponse = await _client.PostAsJsonAsync($"{BaseUrl}/{fiscalYear.Id}/finalize",
            new FinalizeFiscalYearRequest { FinalizedThroughDate = cancellationDate });
        var cancelResponse = await _client.PostAsJsonAsync($"{BaseUrl}/{fiscalYear.Id}/cancel",
            new CancelFiscalYearRequest { CancellationDate = cancellationDate });

        Assert.Equal(HttpStatusCode.OK, openResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, finalizeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
    }

    private async Task<long> CreateBookAsync(string code, string ownerId)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/accounting/books",
            new CreateAccountingBookRequest
            {
                Code = code,
                Title = code,
                OwnerType = "TEST",
                OwnerId = ownerId
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<CreateAccountingBookResponse>(JsonOptions))!.Id;
        var activateResponse = await _client.PostAsync($"/api/v1/accounting/books/{id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        return id;
    }

    private async Task<CreateFiscalYearResponse> CreateFiscalYearAsync(
        long bookId, DateOnly startDate, DateOnly endDate)
    {
        var response = await _client.PostAsJsonAsync(BaseUrl, CreateRequest(bookId, startDate, endDate));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateFiscalYearResponse>(JsonOptions))!;
    }

    private static CreateFiscalYearRequest CreateRequest(long bookId, DateOnly startDate, DateOnly endDate) => new()
    {
        AccountingBookId = bookId,
        Title = "Fiscal year",
        StartDate = startDate,
        EndDate = endDate
    };

    private static async Task AssertProblemAsync(HttpResponseMessage response, string expectedErrorCode)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(expectedErrorCode, problem.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    private sealed record ResolveResponse(long Id);

    private sealed class ProblemDetailsResponse
    {
        [JsonPropertyName("errorCode")]
        public string ErrorCode { get; init; } = null!;

        [JsonPropertyName("traceId")]
        public string TraceId { get; init; } = null!;
    }
}
