using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.IntegrationTests.Common;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Apex.IntegrationTests.Accounting.ChartOfAccounts;

[Collection(ChartOfAccountsHttpTestsCollection.Name)]
public sealed class ChartOfAccountsHttpTests(ChartOfAccountsHttpFixture fixture) : IAsyncLifetime
{
    private const string BaseUrl = "/api/v1/accounting/chart-of-accounts"; private HttpClient _client = null!;
    public Task InitializeAsync() { _client = fixture.Factory.CreateClient(); return Task.CompletedTask; }
    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task All_Endpoints_Should_Complete_A_Hierarchy_Workflow()
    {
        await fixture.ResetAsync();
        var root = await CreateAsync("classes", new { code = "assets", name = "Assets" });
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"{BaseUrl}/classes/{root}", new { name = "Assets renamed" })).StatusCode);
        var general = await CreateAsync("general-accounts", new { accountClassId = root, code = "ca", name = "Cash", nature = 0 });
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"{BaseUrl}/general-accounts/{general}", new { name = "Cash renamed" })).StatusCode);
        var leaf = await CreateAsync("subsidiary-accounts", new { generalAccountId = general, code = "bk", name = "Bank", nature = 0, detailAccountType = 1 });
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"{BaseUrl}/subsidiary-accounts/{leaf}", new { name = "Bank renamed" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"{BaseUrl}/SubsidiaryAccount/{leaf}")).StatusCode);
        var treeResponse = await _client.GetAsync($"{BaseUrl}/tree");
        Assert.True(treeResponse.StatusCode == HttpStatusCode.OK, await treeResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"{BaseUrl}/search?term=renamed&page=1&pageSize=10")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"{BaseUrl}/subsidiary-accounts/{leaf}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"{BaseUrl}/general-accounts/{general}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"{BaseUrl}/classes/{root}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"{BaseUrl}/classes/{root}/reactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"{BaseUrl}/general-accounts/{general}/reactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"{BaseUrl}/subsidiary-accounts/{leaf}/reactivate", null)).StatusCode);
    }

    [Fact]
    public async Task Invalid_Request_And_Missing_Account_Should_Return_Problem_Details()
    {
        await fixture.ResetAsync();
        var invalid = await _client.PostAsJsonAsync($"{BaseUrl}/classes", new { code = "", name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("validation_failed", (await ReadProblemAsync(invalid)).GetProperty("errorCode").GetString());
        var missing = await _client.GetAsync($"{BaseUrl}/AccountClass/999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("account_class_not_found", (await ReadProblemAsync(missing)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Search_Should_Reject_Unbounded_Page_Size()
    {
        var response = await _client.GetAsync($"{BaseUrl}/search?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<long> CreateAsync(string path, object body) { var response = await _client.PostAsJsonAsync($"{BaseUrl}/{path}", body); Assert.Equal(HttpStatusCode.Created, response.StatusCode); var json = await response.Content.ReadFromJsonAsync<JsonElement>(); return json.GetProperty("id").GetInt64(); }
    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();
}

public sealed class ChartOfAccountsHttpFixture : IAsyncLifetime
{
    public ApexWebApplicationFactory Factory { get; } = new(); public Task InitializeAsync() => Factory.InitializeAsync(); public Task DisposeAsync() => Factory.DisposeAsync(); public Task ResetAsync() => Factory.ResetAccountingDatabaseAsync();
}
[CollectionDefinition(Name)] public sealed class ChartOfAccountsHttpTestsCollection : ICollectionFixture<ChartOfAccountsHttpFixture> { public const string Name = "ChartOfAccountsHttpTestsCollection"; }
