using Apex.DatabaseMigrator.Migrations;
using Microsoft.Extensions.Configuration;

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? "Development";

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("AccountingWriteDb");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Connection string 'AccountingWriteDb' is missing.");
    return 1;
}

Console.WriteLine("Running Apex Accounting migrations...");
Console.WriteLine($"Environment: {environment}");

var result = DatabaseMigrationRunner.RunAccountingMigrations(connectionString);

if (!result.Successful)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine(result.Error);
    Console.ResetColor();
    return 1;
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Accounting migrations completed successfully.");
Console.ResetColor();

return 0;
