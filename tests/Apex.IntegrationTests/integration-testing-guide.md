# Apex Integration Testing Guide

This document explains how to write integration tests in Apex.

Apex integration tests use xUnit, Testcontainers, SQL Server containers, DbUp migrations, Dapper, real dependency injection, and real database connections.

The goal is to test real application and infrastructure behavior without depending on a developer-machine database.

---

## Core idea

Apex has two integration-test modes:

| Mode | Base class | Database shape | Use case |
|---|---|---:|---|
| Business integration tests | `ApexIntegrationTestBase` | One SQL Server container | Normal feature tests |
| Infrastructure read/write routing tests | `SeparatedReadWriteIntegrationTestBase` | Two SQL Server containers | Proving read/write physical separation |

Use the single-database base by default.

---

## 1. Default business integration tests

Use:

```csharp
ApexIntegrationTestBase
```

This starts one SQL Server container.

Both logical connection strings point to the same physical database:

```text
AccountingReadDb  == AccountingWriteDb
```

This is the correct mode for business tests because reads and writes must observe the same state.

Use it for:

- accounting feature tests
- repository tests
- handler tests
- endpoint tests
- transaction tests
- tests that need real migrated schema
- tests where read-after-write consistency matters

Example:

```csharp
namespace Apex.IntegrationTests.Accounting;

using Apex.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;

public sealed class FiscalYearTests : ApexIntegrationTestBase
{
    public FiscalYearTests(ApexIntegrationTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task CreateFiscalYear_Should_Be_Readable()
    {
        await ResetAccountingDatabaseAsync();

        await using var scope = await CreateScopeAsync();

        // Resolve application services from scope.Services.
        // Execute behavior.
        // Assert persisted state.
    }
}
```

---

## 2. Separated read/write infrastructure tests

Use:

```csharp
SeparatedReadWriteIntegrationTestBase
```

This starts two SQL Server containers:

```text
AccountingReadDb  != AccountingWriteDb
```

Use this only when explicitly testing infrastructure routing.

Use it for:

- proving `IReadDbConnectionFactory` uses the read DB
- proving `IWriteDbConnectionFactory` uses the write DB
- proving `IWriteTransactionRunner` writes only to the write DB
- database resolver tests that require physical separation

Do not use this for normal business tests.

Example:

```csharp
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
}
```

---

## Rule of thumb

Use this by default:

```csharp
ApexIntegrationTestBase
```

Use this only for infrastructure separation tests:

```csharp
SeparatedReadWriteIntegrationTestBase
```

Business tests should normally use a single physical database because reads and writes should see the same data state.

---

## DbUp migrations

Integration tests do not manually create tables.

All schema must come from DbUp migrations.

The test fixture runs migrations using:

```csharp
DatabaseMigrationRunner.RunAccountingMigrations(connectionString);
```

This means integration tests use the same migration mechanism as the real application.

Do not put schema-creation SQL directly inside tests.

Bad:

```csharp
await connection.ExecuteAsync("""
    CREATE TABLE accounting_fiscal_years (...)
    """);
```

Good:

```text
tools/Apex.DatabaseMigrator/Scripts/Accounting/000003_create_accounting_fiscal_years.sql
```

---

## Test data setup

Tests may insert or update test data, but they should not create schema.

Allowed:

```csharp
await connection.ExecuteAsync("""
    INSERT INTO accounting_fiscal_years(
        id,
        title,
        start_date,
        end_date,
        status,
        created_at
    )
    VALUES (
        @Id,
        @Title,
        @StartDate,
        @EndDate,
        @Status,
        SYSUTCDATETIME()
    )
    """, payload);
```

Not allowed:

```csharp
await connection.ExecuteAsync("""
    CREATE TABLE accounting_fiscal_years (...)
    """);
```

---

## Resetting database state

Each test should clean the data it mutates.

For normal business tests, call this at the beginning of the test when needed:

```csharp
await ResetAccountingDatabaseAsync();
```

Example:

```csharp
[Fact]
public async Task ReadAndWrite_Should_See_Same_State()
{
    await ResetAccountingDatabaseAsync();

    await using var scope = await CreateScopeAsync();

    // test body
}
```

The reset method should stay conservative:

- delete test data from known mutable tables
- do not drop schema
- do not recreate database
- do not rerun migrations per test

---

## Resolving services

Always resolve scoped services from a scope.

Good:

```csharp
await using var scope = await CreateScopeAsync();

var runner = scope.Services.GetRequiredService<IWriteTransactionRunner>();
var session = scope.Services.GetRequiredService<IWriteDbSession>();
```

Avoid resolving scoped services from the root provider.

Bad:

```csharp
await using var provider = CreateServiceProvider();

var writeFactory = provider.GetRequiredService<IWriteDbConnectionFactory>();
```

This may fail with:

```text
Cannot resolve scoped service from root provider.
```

Singleton services may be resolved from the root provider, but using a scope is usually safer and more consistent.

---

## Testing write transactions

Use `IWriteTransactionRunner` and `IWriteDbSession`.

Commit example:

```csharp
[Fact]
public async Task WriteTransactionRunner_Should_Commit()
{
    await ResetAccountingDatabaseAsync();

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

    await using var connection = CreateAccountingConnection();
    await connection.OpenAsync();

    var count = await connection.ExecuteScalarAsync<int>(
        "SELECT COUNT(*) FROM write_transaction_test WHERE id = 1");

    Assert.Equal(1, count);
}
```

Rollback example:

```csharp
[Fact]
public async Task WriteTransactionRunner_Should_Rollback()
{
    await ResetAccountingDatabaseAsync();

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

    await using var connection = CreateAccountingConnection();
    await connection.OpenAsync();

    var count = await connection.ExecuteScalarAsync<int>(
        "SELECT COUNT(*) FROM write_transaction_test WHERE id = 2");

    Assert.Equal(0, count);
}
```

---

## Opening direct database connections

For normal business tests:

```csharp
await using var connection = CreateAccountingConnection();
await connection.OpenAsync();
```

For separated read/write infrastructure tests:

```csharp
await using var readConnection = CreateAccountingReadConnection();
await using var writeConnection = CreateAccountingWriteConnection();

await readConnection.OpenAsync();
await writeConnection.OpenAsync();
```

Prefer application services for behavior tests. Use direct SQL mainly for arranging data or asserting persisted state.

---

## Running integration tests

Run all integration tests:

```bash
TESTCONTAINERS_RYUK_DISABLED=true \
dotnet test tests/Apex.IntegrationTests/Apex.IntegrationTests.csproj
```

Run one test class:

```bash
TESTCONTAINERS_RYUK_DISABLED=true \
dotnet test tests/Apex.IntegrationTests/Apex.IntegrationTests.csproj \
  --filter "FullyQualifiedName~ReadWriteSeparationTests"
```

Run one business test class:

```bash
TESTCONTAINERS_RYUK_DISABLED=true \
dotnet test tests/Apex.IntegrationTests/Apex.IntegrationTests.csproj \
  --filter "FullyQualifiedName~AccountingSingleDatabaseTests"
```

---

## Ryuk

Testcontainers normally uses Ryuk as a cleanup sidecar container.

If Docker Hub access causes problems, run tests with:

```bash
TESTCONTAINERS_RYUK_DISABLED=true
```

When Ryuk is disabled, containers are still normally disposed by the test fixture. However, if the test process crashes, orphan containers may remain.

Clean them manually:

```bash
docker rm -f $(docker ps -aq --filter "label=org.testcontainers=true")
```

---

## Performance notes

Testcontainers are expensive to start.

Do not start containers per test method.

Use xUnit collection fixtures so the same container is reused for all tests in the collection.

Correct:

```csharp
[CollectionDefinition(ApexIntegrationTestCollection.Name)]
public sealed class ApexIntegrationTestCollection
    : ICollectionFixture<ApexIntegrationTestFixture>
{
    public const string Name = "ApexIntegrationTestCollection";
}
```

Wrong:

```csharp
public sealed class SomeTests : IAsyncLifetime
{
    // Starts container per test method because xUnit creates a new test class instance per test.
}
```

---

## Recommended folder structure

```text
tests/Apex.IntegrationTests/
  Common/
    ApexIntegrationTestBase.cs
    ApexIntegrationTestCollection.cs
    ApexIntegrationTestFixture.cs

  Accounting/
    FiscalYearTests.cs
    JournalEntryTests.cs
    ChartOfAccountsTests.cs

  Infrastructure/
    Data/
      SeparatedReadWriteIntegrationTestBase.cs
      SeparatedReadWriteIntegrationTestCollection.cs
      SeparatedReadWriteIntegrationTestFixture.cs
      ReadWriteSeparationTests.cs
```

---

## Recommended test naming

Use behavior-focused names:

```csharp
CreateFiscalYear_Should_Persist_FiscalYear()
CreateJournalEntry_Should_Rollback_When_DebitAndCreditAreNotBalanced()
ReadFactory_Should_Use_AccountingReadDb()
WriteTransactionRunner_Should_Rollback_On_Exception()
```

Avoid vague names:

```csharp
Test1()
FiscalYearTest()
DatabaseWorks()
```

---

## Checklist for a new business integration test

1. Put the test under `tests/Apex.IntegrationTests/Accounting`.
2. Inherit from `ApexIntegrationTestBase`.
3. Call `await ResetAccountingDatabaseAsync();` when the test mutates data.
4. Resolve scoped services from `await CreateScopeAsync();`.
5. Use application services for behavior.
6. Use direct SQL only for arrange/assert.
7. Make sure all required schema exists through DbUp migrations.
8. Keep the test deterministic.

---

## Checklist for a new infrastructure read/write test

1. Put the test under `tests/Apex.IntegrationTests/Infrastructure/Data`.
2. Inherit from `SeparatedReadWriteIntegrationTestBase`.
3. Use `CreateAccountingReadConnection()` to inspect read DB state.
4. Use `CreateAccountingWriteConnection()` to inspect write DB state.
5. Do not use this base for business behavior tests.
6. Assert routing behavior explicitly.

---

## Final rules

1. Use `ApexIntegrationTestBase` for normal business tests.
2. Use `SeparatedReadWriteIntegrationTestBase` only for read/write infrastructure tests.
3. Do not create schema manually in tests.
4. Put schema changes in DbUp migrations.
5. Resolve scoped services from `CreateScopeAsync()`.
6. Reset mutated data at the beginning of each test.
7. Prefer direct SQL only for arrange/assert, not for business behavior.
8. Keep tests deterministic and isolated.
