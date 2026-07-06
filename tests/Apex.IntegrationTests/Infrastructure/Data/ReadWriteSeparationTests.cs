namespace Apex.IntegrationTests.Infrastructure.Data;

using Apex.Application.Abstractions.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

public sealed class ReadWriteSeparationTests : SeparatedReadWriteIntegrationTestBase
{
    public ReadWriteSeparationTests(SeparatedReadWriteIntegrationTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ReadFactory_Should_Use_AccountingReadDb()
    {
        await using var provider = CreateServiceProvider();

        var readFactory = provider.GetRequiredService<IReadDbConnectionFactory>();

        await using var connection = await readFactory.OpenConnectionAsync("Accounting");

        var marker = await connection.ExecuteScalarAsync<string>(
            "SELECT TOP 1 name FROM db_marker");

        Assert.Equal("READ_DATABASE", marker);
    }

    [Fact]
    public async Task WriteFactory_Should_Use_AccountingWriteDb()
    {
        await using var scope = await CreateScopeAsync();

        var writeFactory = scope.Services.GetRequiredService<IWriteDbConnectionFactory>();

        await using var connection = await writeFactory.OpenConnectionAsync("Accounting");

        var marker = await connection.ExecuteScalarAsync<string>(
            "SELECT TOP 1 name FROM db_marker");

        Assert.Equal("WRITE_DATABASE", marker);
    }

    [Fact]
    public async Task WriteTransactionRunner_Should_Commit_To_WriteDatabase()
    {
        await ResetAccountingWriteDatabaseAsync();

        await using var scope = await CreateScopeAsync();

        var runner = scope.Services.GetRequiredService<IWriteTransactionRunner>();
        var session = scope.Services.GetRequiredService<IWriteDbSession>();

        await runner.ExecuteAsync("Accounting", async ct =>
        {
            await session.Connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO write_transaction_test(id, name)
                    VALUES (1, 'committed')
                    """,
                    transaction: session.Transaction,
                    cancellationToken: ct));
        });

        await using var writeConnection = CreateAccountingWriteConnection();
        await writeConnection.OpenAsync();

        var count = await writeConnection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM write_transaction_test WHERE id = 1");

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task WriteTransactionRunner_Should_Rollback_On_Exception()
    {
        await ResetAccountingWriteDatabaseAsync();

        await using var scope = await CreateScopeAsync();

        var runner = scope.Services.GetRequiredService<IWriteTransactionRunner>();
        var session = scope.Services.GetRequiredService<IWriteDbSession>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.ExecuteAsync("Accounting", async ct =>
            {
                await session.Connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO write_transaction_test(id, name)
                        VALUES (2, 'rolled-back')
                        """,
                        transaction: session.Transaction,
                        cancellationToken: ct));

                throw new InvalidOperationException("force rollback");
            }));

        await using var writeConnection = CreateAccountingWriteConnection();
        await writeConnection.OpenAsync();

        var count = await writeConnection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM write_transaction_test WHERE id = 2");

        Assert.Equal(0, count);
    }
}
