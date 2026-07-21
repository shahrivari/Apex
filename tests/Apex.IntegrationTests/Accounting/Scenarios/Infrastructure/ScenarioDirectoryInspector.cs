using Dapper;
using Microsoft.Data.SqlClient;

namespace Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;

public sealed record ShardAssignmentInspection(
    string EntityType, string Discriminator, string ShardId, string Status);

public sealed class ScenarioDirectoryInspector(string generalConnectionString)
{
    public async Task<ShardAssignmentInspection?> GetFiscalYearAssignmentAsync(
        long fiscalYearId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ShardAssignmentInspection>(new CommandDefinition(
            """
            SELECT entity_type AS EntityType, discriminator AS Discriminator,
                   shard_id AS ShardId, status AS Status
            FROM ShardAssignments
            WHERE entity_type = 'FiscalYear' AND discriminator = @Discriminator
            """,
            new { Discriminator = fiscalYearId.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            cancellationToken: cancellationToken));
    }

    public async Task SetShardStatusAsync(
        string connectionName, string status, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Shards SET status = @Status WHERE connection_name = @ConnectionName",
            new { ConnectionName = connectionName, Status = status },
            cancellationToken: cancellationToken));
    }

    public async Task DeleteFiscalYearDirectoryRowAsync(
        long fiscalYearId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        Assert.Equal(1, await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM fiscal_year_directory WHERE id = @Id",
            new { Id = fiscalYearId }, cancellationToken: cancellationToken)));
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(generalConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
