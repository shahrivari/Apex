using Apex.DatabaseMigrator.Migrations;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Testcontainers.MsSql;

namespace Apex.IntegrationTests.Http;

public class ApexWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string AccountingConnectionString => _sqlContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        RunMigrations();
    }

    public new async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    public async Task ResetAccountingDatabaseAsync()
    {
        await using var connection = new SqlConnection(AccountingConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(@"
            IF OBJECT_ID('accounting_book', 'U') IS NOT NULL
                DELETE FROM accounting_book;
        ");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sharding:GeneralConnectionStringName"] = "GeneralDb",
                ["Sharding:RequiredSchemaVersion"] = "1",
                ["ConnectionStrings:GeneralDb"] = AccountingConnectionString
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.AuthenticationScheme,
                    _ => { });
        });
    }

    void RunMigrations()
    {
        var result = DatabaseMigrationRunner.RunGeneralMigrations(AccountingConnectionString);
        if (!result.Successful)
            throw new InvalidOperationException("Database migration failed.", result.Error);
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string AuthenticationScheme = "IntegrationTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "integration-test-user")],
                AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, AuthenticationScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
