using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.IntegrationTests.Common;
using Apex.IntegrationTests.Http;
using Apex.Modules.Accounting.DetailAccounts.UseCases.CreateDetailAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.GetDetailAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.ListDetailAccounts;
using Apex.Modules.Accounting.DetailAccounts.UseCases.SearchDetailAccountsForPosting;
using Apex.Modules.Accounting.DetailAccounts.UseCases.UpdateDetailAccount;

namespace Apex.IntegrationTests.Accounting.DetailAccounts;

[Collection(ApexHttpIntegrationTestCollection.Name)]
public sealed class DetailAccountHttpTests(ApexWebApplicationFactory factory) : IAsyncLifetime
{
    private const string BaseUrl = "/api/v1/accounting/detail-accounts";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _client = factory.CreateClient();
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
    public async Task CreateGetByIdAndGetByCode_ShouldReturnPublicContracts()
    {
        await factory.ResetAccountingDatabaseAsync();
        var create = await _client.PostAsJsonAsync(
            BaseUrl,
            new CreateDetailAccountRequest(" http-person ", "HTTP Person", "PERSON")
        );
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = Assert.IsType<CreateDetailAccountResponse>(
            await create.Content.ReadFromJsonAsync<CreateDetailAccountResponse>(JsonOptions)
        );
        Assert.Equal(
            ("HTTP-PERSON", "HTTP Person", "PERSON", "ACTIVE"),
            (created.Code, created.Name, created.Type, created.Status)
        );
        Assert.Equal(new Uri($"{BaseUrl}/{created.Id}", UriKind.Relative), create.Headers.Location);

        var byIdResponse = await _client.GetAsync($"{BaseUrl}/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, byIdResponse.StatusCode);
        var byId = Assert.IsType<GetDetailAccountResponse>(
            await byIdResponse.Content.ReadFromJsonAsync<GetDetailAccountResponse>(JsonOptions)
        );
        Assert.Equal(created.Id, byId.Id);

        var byCodeResponse = await _client.GetAsync($"{BaseUrl}/by-code/http-person");
        Assert.Equal(HttpStatusCode.OK, byCodeResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateListAndPostingSearch_ShouldApplyFiltersAndEligibility()
    {
        await factory.ResetAccountingDatabaseAsync();
        var person = await CreateAsync("FILTER-PERSON", "Alpha Person", "PERSON");
        await CreateAsync("FILTER-BANK", "Alpha Bank", "BANK");

        var update = await _client.PutAsJsonAsync(
            $"{BaseUrl}/{person.Id}",
            new UpdateDetailAccountRequest("Renamed Person", "SYMBOL", person.Code)
        );
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var listResponse = await _client.GetAsync(
            $"{BaseUrl}?type=SYMBOL&status=ACTIVE&search=renamed&page=1&pageSize=10"
        );
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = Assert.IsType<ListDetailAccountsResponse>(
            await listResponse.Content.ReadFromJsonAsync<ListDetailAccountsResponse>(JsonOptions)
        );
        Assert.Equal(1, list.TotalCount);
        Assert.Equal(person.Id, Assert.Single(list.Items).Id);

        var searchResponse = await _client.GetAsync(
            $"{BaseUrl}/posting-search?type=SYMBOL&search=renamed&limit=10"
        );
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        var search = Assert.IsType<SearchDetailAccountsForPostingResponse>(
            await searchResponse.Content.ReadFromJsonAsync<SearchDetailAccountsForPostingResponse>(
                JsonOptions
            )
        );
        Assert.Equal(person.Code, Assert.Single(search.Items).Code);
    }

    [Fact]
    public async Task ArchiveAndReactivate_ShouldChangePostingSearchAndRejectInvalidTransitions()
    {
        await factory.ResetAccountingDatabaseAsync();
        var created = await CreateAsync("HTTP-LIFE", "Lifecycle", "BANK");

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _client.PostAsync($"{BaseUrl}/{created.Id}/archive", null)).StatusCode
        );
        var search = await _client.GetFromJsonAsync<SearchDetailAccountsForPostingResponse>(
            $"{BaseUrl}/posting-search?type=BANK&search=HTTP-LIFE",
            JsonOptions
        );
        Assert.Empty(Assert.IsType<SearchDetailAccountsForPostingResponse>(search).Items);

        var archiveAgain = await _client.PostAsync($"{BaseUrl}/{created.Id}/archive", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, archiveAgain.StatusCode);
        await AssertProblemAsync(archiveAgain, "detail_account_already_archived");

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _client.PostAsync($"{BaseUrl}/{created.Id}/reactivate", null)).StatusCode
        );
        var reactivateAgain = await _client.PostAsync($"{BaseUrl}/{created.Id}/reactivate", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reactivateAgain.StatusCode);
        await AssertProblemAsync(reactivateAgain, "detail_account_already_active");
    }

    [Fact]
    public async Task ValidationConflictImmutableAndNotFound_ShouldReturnStableProblems()
    {
        await factory.ResetAccountingDatabaseAsync();
        var invalid = await _client.PostAsJsonAsync(
            BaseUrl,
            new CreateDetailAccountRequest("", "", "OTHER")
        );
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await AssertProblemAsync(invalid, "validation_failed");

        var created = await CreateAsync("HTTP-ERROR", "Original", "PERSON");
        var duplicate = await _client.PostAsJsonAsync(
            BaseUrl,
            new CreateDetailAccountRequest(" http-error ", "Duplicate", "BANK")
        );
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await AssertProblemAsync(duplicate, "detail_account_code_already_exists");

        var immutable = await _client.PutAsJsonAsync(
            $"{BaseUrl}/{created.Id}",
            new UpdateDetailAccountRequest("Changed", "PERSON", "OTHER")
        );
        Assert.Equal(HttpStatusCode.UnprocessableEntity, immutable.StatusCode);
        await AssertProblemAsync(immutable, "detail_account_code_immutable");

        var missing = await _client.GetAsync($"{BaseUrl}/{long.MaxValue}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        await AssertProblemAsync(missing, "detail_account_not_found");
    }

    [Fact]
    public async Task Delete_ShouldReturn204RemoveAccountAndReserveCode()
    {
        await factory.ResetAccountingDatabaseAsync();
        var created = await CreateAsync("HTTP-DELETE", "Delete", "SYMBOL");
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _client.DeleteAsync($"{BaseUrl}/{created.Id}")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _client.GetAsync($"{BaseUrl}/{created.Id}")).StatusCode
        );
        var reuse = await _client.PostAsJsonAsync(
            BaseUrl,
            new CreateDetailAccountRequest("HTTP-DELETE", "Reuse", "SYMBOL")
        );
        Assert.Equal(HttpStatusCode.Conflict, reuse.StatusCode);
    }

    private async Task<CreateDetailAccountResponse> CreateAsync(
        string code,
        string name,
        string type
    )
    {
        var response = await _client.PostAsJsonAsync(
            BaseUrl,
            new CreateDetailAccountRequest(code, name, type)
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return Assert.IsType<CreateDetailAccountResponse>(
            await response.Content.ReadFromJsonAsync<CreateDetailAccountResponse>(JsonOptions)
        );
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, string errorCode)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(errorCode, problem.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    private sealed record ProblemResponse(string ErrorCode, string TraceId);
}
