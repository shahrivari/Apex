using Apex.DatabaseMigrator.Migrations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var generalConnectionName = configuration["Sharding:GeneralConnectionStringName"]
    ?? "GeneralDb";
var requiredSchemaVersion = configuration["Sharding:RequiredSchemaVersion"] ?? "1";
var generalConnectionString = configuration.GetConnectionString(generalConnectionName);

if (string.IsNullOrWhiteSpace(generalConnectionString))
{
    Console.Error.WriteLine($"General connection string '{generalConnectionName}' is missing.");
    return 1;
}

Console.WriteLine($"Running general migrations for environment '{environment}'...");
var generalResult = DatabaseMigrationRunner.RunGeneralMigrations(generalConnectionString);
if (!generalResult.Successful)
{
    Console.Error.WriteLine(generalResult.Error);
    return 1;
}

var shards = new List<(string Id, string ConnectionName)>();
await using (var connection = new SqlConnection(generalConnectionString))
{
    await connection.OpenAsync();
    await using var command = new SqlCommand(
        "SELECT id, connection_name FROM Shards WHERE status <> 'FAILED' ORDER BY id",
        connection);
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        shards.Add((reader.GetString(0), reader.GetString(1)));
}

var failed = false;
foreach (var shard in shards)
{
    var shardConnectionString = configuration.GetConnectionString(shard.ConnectionName);
    if (string.IsNullOrWhiteSpace(shardConnectionString))
    {
        Console.Error.WriteLine(
            $"Shard '{shard.Id}' references unknown connection '{shard.ConnectionName}'.");
        failed = true;
        continue;
    }

    Console.WriteLine($"Running shard migrations for '{shard.Id}'...");
    var result = DatabaseMigrationRunner.RunShardMigrations(shardConnectionString);
    if (!result.Successful)
    {
        Console.Error.WriteLine(result.Error);
        failed = true;
        continue;
    }

    await using var general = new SqlConnection(generalConnectionString);
    await general.OpenAsync();
    await using var update = new SqlCommand(
        """
        UPDATE Shards
        SET schema_version = @SchemaVersion,
            modified_at = SYSUTCDATETIME()
        WHERE id = @ShardId
        """,
        general);
    update.Parameters.AddWithValue("@SchemaVersion", requiredSchemaVersion);
    update.Parameters.AddWithValue("@ShardId", shard.Id);
    await update.ExecuteNonQueryAsync();
}

if (failed)
    return 1;

Console.WriteLine("General and shard migrations completed successfully.");
return 0;
