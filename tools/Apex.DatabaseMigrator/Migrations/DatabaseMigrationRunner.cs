namespace Apex.DatabaseMigrator.Migrations;

using DbUp;
using DbUp.Engine;

public static class DatabaseMigrationRunner
{
    public static DatabaseUpgradeResult RunGeneralMigrations(
        string connectionString)
    {
        return RunMigrations(
            connectionString,
            script => script.Contains(".Scripts.General.", StringComparison.Ordinal));
    }

    public static DatabaseUpgradeResult RunShardMigrations(
        string connectionString)
    {
        return RunMigrations(
            connectionString,
            script => script.Contains(".Scripts.Shard.", StringComparison.Ordinal));
    }

    public static DatabaseUpgradeResult RunTestMigrations(
        string connectionString)
    {
        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(DatabaseMigrationRunner).Assembly,
                script => script.Contains(".Scripts.Test.Accounting."))
            .LogToConsole()
            .WithTransactionPerScript()
            .Build();

        return upgrader.PerformUpgrade();
    }

    private static DatabaseUpgradeResult RunMigrations(
        string connectionString,
        Func<string, bool> scriptFilter)
    {
        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(DatabaseMigrationRunner).Assembly,
                scriptFilter)
            .LogToConsole()
            .WithTransactionPerScript()
            .Build();

        return upgrader.PerformUpgrade();
    }
}
