namespace Apex.DatabaseMigrator.Migrations;

using DbUp;
using DbUp.Engine;

public static class DatabaseMigrationRunner
{
    public static DatabaseUpgradeResult RunAccountingMigrations(
        string connectionString)
    {
        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(DatabaseMigrationRunner).Assembly,
                script => script.Contains(".Scripts.Accounting."))
            .LogToConsole()
            .WithTransactionPerScript()
            .Build();

        return upgrader.PerformUpgrade();
    }
}
