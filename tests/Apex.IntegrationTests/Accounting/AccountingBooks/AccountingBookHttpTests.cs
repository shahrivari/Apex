#pragma warning disable CS8602

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Data.SqlClient;
using Apex.IntegrationTests.Http;
using Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;

namespace Apex.IntegrationTests.Accounting.AccountingBooks;

[Collection("AccountingBookHttpTestsCollection")]
public sealed class AccountingBookHttpTests : IAsyncLifetime
{
    private const string BaseUrl = "/api/v1/accounting/books";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
    };

    private readonly AccountingBookHttpTestsFixture _fixture;
    private HttpClient _client = null!;

    public AccountingBookHttpTests(AccountingBookHttpTestsFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _client = _fixture.Factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Get_Existing_Should_Return_200()
    {
        await ArrangeCreateBookAsync("smoke-get");
        await using var conn = _fixture.CreateAccountingConnection();
        await conn.OpenAsync();
        var id = await conn.ExecuteScalarAsync<long>("SELECT TOP 1 id FROM accounting_book ORDER BY id DESC");

        var response = await _client.GetAsync($"{BaseUrl}/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_NotFound_Should_Return_404()
    {
        var response = await _client.GetAsync($"{BaseUrl}/99999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal("accounting_book_not_found", problem!.ErrorCode);
    }

    [Fact]
    public async Task List_Empty_Should_Return_200()
    {
        await _fixture.ResetAccountingDatabaseAsync();

        var response = await _client.GetAsync(BaseUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task List_WithParams_Should_Return_200()
    {
        await ArrangeCreateBookAsync("smoke-list");

        var response = await _client.GetAsync($"{BaseUrl}?page=1&pageSize=5&status=DRAFT");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Return_201()
    {
        await _fixture.ResetAccountingDatabaseAsync();

        var response = await _client.PostAsJsonAsync(
            BaseUrl,
            new CreateAccountingBookRequest
            {
                Code = "smoke-create",
                Title = "Smoke Test",
                OwnerType = "PORTFOLIO",
                OwnerId = "999"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateAccountingBookResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("SMOKE-CREATE", body!.Code);
    }

    [Fact]
    public async Task Activate_Should_Return_200()
    {
        await ArrangeCreateBookAsync("smoke-activate");
        await using var conn = _fixture.CreateAccountingConnection();
        await conn.OpenAsync();
        var id = await conn.ExecuteScalarAsync<long>("SELECT TOP 1 id FROM accounting_book ORDER BY id DESC");

        var response = await _client.PostAsync($"{BaseUrl}/{id}/activate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Activate_NotFound_Should_Return_404()
    {
        var response = await _client.PostAsync($"{BaseUrl}/99999999/activate", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal("accounting_book_not_found", problem!.ErrorCode);
    }

    [Fact]
    public async Task Suspend_Should_Return_200()
    {
        await ArrangeCreateBookAsync("smoke-suspend");
        await using var conn = _fixture.CreateAccountingConnection();
        await conn.OpenAsync();
        var id = await conn.ExecuteScalarAsync<long>("SELECT TOP 1 id FROM accounting_book ORDER BY id DESC");
        await conn.ExecuteAsync("UPDATE accounting_book SET status = 'ACTIVE' WHERE id = @Id", new { Id = id });

        var response = await _client.PostAsync($"{BaseUrl}/{id}/suspend", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Suspend_NotFound_Should_Return_404()
    {
        var response = await _client.PostAsync($"{BaseUrl}/99999999/suspend", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal("accounting_book_not_found", problem!.ErrorCode);
    }

    [Fact]
    public async Task Archive_Should_Return_200()
    {
        await ArrangeCreateBookAsync("smoke-archive");
        await using var conn = _fixture.CreateAccountingConnection();
        await conn.OpenAsync();
        var id = await conn.ExecuteScalarAsync<long>("SELECT TOP 1 id FROM accounting_book ORDER BY id DESC");

        var response = await _client.PostAsync($"{BaseUrl}/{id}/archive", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Archive_NotFound_Should_Return_404()
    {
        var response = await _client.PostAsync($"{BaseUrl}/99999999/archive", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal("accounting_book_not_found", problem!.ErrorCode);
    }

    private async Task ArrangeCreateBookAsync(string code)
    {
        await _fixture.ResetAccountingDatabaseAsync();

        var response = await _client.PostAsJsonAsync(
            BaseUrl,
            new CreateAccountingBookRequest
            {
                Code = code,
                Title = "Smoke Arrange",
                OwnerType = "PORTFOLIO",
                OwnerId = "999"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    public sealed class ProblemDetailsDto
    {
        [JsonPropertyName("errorCode")]
        public string ErrorCode { get; set; } = null!;
    }
}

public sealed class AccountingBookHttpTestsFixture : IAsyncLifetime
{
    private readonly ApexWebApplicationFactory _factory = new();

    public ApexWebApplicationFactory Factory => _factory;

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    public SqlConnection CreateAccountingConnection()
    {
        return new SqlConnection(_factory.AccountingConnectionString);
    }

    public Task ResetAccountingDatabaseAsync()
    {
        return _factory.ResetAccountingDatabaseAsync();
    }
}

[CollectionDefinition("AccountingBookHttpTestsCollection")]
public sealed class AccountingBookHttpTestsCollection
    : ICollectionFixture<AccountingBookHttpTestsFixture>
{
}
