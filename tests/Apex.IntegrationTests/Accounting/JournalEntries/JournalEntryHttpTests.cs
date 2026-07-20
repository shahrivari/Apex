using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Apex.IntegrationTests.Common;
using Apex.IntegrationTests.Http;
using Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;
using Apex.Modules.Accounting.FiscalYears.UseCases.CreateFiscalYear;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using Apex.Modules.Accounting.JournalEntries.UseCases.CreateDraftJournalEntry;

namespace Apex.IntegrationTests.Accounting.JournalEntries;

[Collection(ApexHttpIntegrationTestCollection.Name)]
public sealed class JournalEntryHttpTests : IAsyncLifetime
{
    private const string BaseUrl = "/api/v1/accounting/journal-entries";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
    };

    private readonly ApexWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public JournalEntryHttpTests(ApexWebApplicationFactory factory) => _factory = factory;

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
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}?fiscalYearId=1");
        request.Headers.Add("X-Test-Unauthenticated", "true");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndGet_ShouldReturnPublicContract()
    {
        await ResetAsync();
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync("JE-HTTP-CREATE", "je-http-create");

        var createResponse = await _client.PostAsJsonAsync(BaseUrl, DraftRequest(bookId, fiscalYearId), JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<EntryResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("DRAFT", created.Status);
        Assert.Equal(fiscalYearId, created.FiscalYearId);
        Assert.Equal(1, created.ReferenceNumber);
        Assert.Equal(2, created.Lines.Count);
        Assert.Equal(new Uri($"{BaseUrl}/{fiscalYearId}/{created.Id}", UriKind.Relative),
            createResponse.Headers.Location);

        var getResponse = await _client.GetAsync($"{BaseUrl}/{fiscalYearId}/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<EntryResponse>(JsonOptions);
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task Create_WithoutLines_ShouldReturnValidationProblem()
    {
        await ResetAsync();
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync("JE-HTTP-INVALID", "je-http-invalid");
        var request = new CreateDraftJournalEntryRequest
        {
            AccountingBookId = bookId,
            FiscalYearId = fiscalYearId,
            AccountingDate = new DateOnly(2026, 6, 1),
            Description = "Journal entry",
            DocumentType = "GENERAL",
            InsertionType = "MANUAL",
            BalanceEffect = "FINANCIAL",
            Lines = []
        };

        var response = await _client.PostAsJsonAsync(BaseUrl, request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemAsync(response, "validation_failed");
    }

    [Fact]
    public async Task MissingEntry_ShouldReturnNotFoundProblem()
    {
        await ResetAsync();
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync("JE-HTTP-MISSING", "je-http-missing");
        // Create one entry so the fiscal year has a shard assignment; then query a missing id on the
        // same (routable) shard so the miss is a genuine not-found rather than a routing failure.
        var createResponse = await _client.PostAsJsonAsync(BaseUrl, DraftRequest(bookId, fiscalYearId), JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var response = await _client.GetAsync($"{BaseUrl}/{fiscalYearId}/999999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, "journal_entry_not_found");
    }

    private async Task ResetAsync()
    {
        await _factory.ResetAccountingDatabaseAsync();
        await _factory.ResetShardDatabaseAsync();
    }

    private static CreateDraftJournalEntryRequest DraftRequest(long bookId, long fiscalYearId) => new()
    {
        AccountingBookId = bookId,
        FiscalYearId = fiscalYearId,
        AccountingDate = new DateOnly(2026, 6, 1),
        Description = "Journal entry",
        DocumentType = "GENERAL",
        InsertionType = "MANUAL",
        BalanceEffect = "FINANCIAL",
        Lines =
        [
            new JournalEntryLineRequest
            {
                Side = "DEBIT", Amount = 100m,
                AccountClassCode = "1", GeneralAccountCode = "01", SubsidiaryAccountCode = "01", Description = "debit"
            },
            new JournalEntryLineRequest
            {
                Side = "CREDIT", Amount = 100m,
                AccountClassCode = "1", GeneralAccountCode = "01", SubsidiaryAccountCode = "01", Description = "credit"
            }
        ]
    };

    private async Task<(long BookId, long FiscalYearId)> CreateOpenFiscalYearAsync(string code, string ownerId)
    {
        var bookResponse = await _client.PostAsJsonAsync("/api/v1/accounting/books",
            new CreateAccountingBookRequest { Code = code, Title = code, OwnerType = "TEST", OwnerId = ownerId },
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, bookResponse.StatusCode);
        var bookId = (await bookResponse.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsync($"/api/v1/accounting/books/{bookId}/activate", null)).StatusCode);

        var fiscalYearResponse = await _client.PostAsJsonAsync("/api/v1/accounting/fiscal-years",
            new CreateFiscalYearRequest
            {
                AccountingBookId = bookId,
                Title = "2026",
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31)
            }, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, fiscalYearResponse.StatusCode);
        var fiscalYearId = (await fiscalYearResponse.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsync($"/api/v1/accounting/fiscal-years/{fiscalYearId}/open", null)).StatusCode);

        return (bookId, fiscalYearId);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, string expectedErrorCode)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(expectedErrorCode, problem.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    private sealed record IdResponse(long Id);

    private sealed record EntryResponse(
        long Id, long FiscalYearId, long ReferenceNumber, string Status, IReadOnlyList<LineResponse> Lines);

    private sealed record LineResponse(int RowNumber, string Side, decimal Amount);

    private sealed class ProblemDetailsResponse
    {
        [JsonPropertyName("errorCode")]
        public string ErrorCode { get; init; } = null!;

        [JsonPropertyName("traceId")]
        public string TraceId { get; init; } = null!;
    }
}
