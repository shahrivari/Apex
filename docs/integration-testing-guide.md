# Apex Integration Testing Guide

This document explains how to write integration tests in Apex.

Apex integration tests use xUnit, Testcontainers, SQL Server containers, DbUp migrations, Dapper, real dependency injection, and real database connections.

The goal is to test real application and infrastructure behavior without depending on a developer-machine database.

---

## Testing layers

Apex uses three testing layers, each with a clear responsibility:

| Layer | What it tests | Speed | Tooling |
|---|---|---|---|
| **HTTP smoke tests** | Endpoint binding, `[AsParameters]`, status codes, `ProblemDetails` error shape | Slow | `WebApplicationFactory`, real HTTP |
| **Handler integration tests** | Handlers → transactor → real DB. Business logic, transactions, conflicts, complex scenarios | Medium | `ApexIntegrationTestBase`, real DI, real DB |
| **Domain unit tests** | State machine rules, invariants, value objects | Fast | Pure C#, no DB, no DI |

**Philosophy:** HTTP tests are thin (one test per endpoint, success + 404). All business logic testing lives in handler integration tests where you get real DB behavior without HTTP overhead. Domain tests are reserved for state machine rules that don't need a database.

---

## 1. HTTP smoke tests

HTTP tests verify that an endpoint is reachable, parameters bind correctly, and the expected status codes are returned. They catch issues like `[AsParameters]` required-parameter mismatches, serialization problems, and middleware errors, which handler tests cannot see.

### How many tests per endpoint

One success test and one 404 test (if the endpoint takes an ID):

| Endpoint | Test | Expected |
|---|---|---|
| `POST /api/v1/accounting/books` | `Create_Should_Return_201` | 201, body deserializes |
| `GET /api/v1/accounting/books/{id}` | `Get_Existing_Should_Return_200` | 200 |
| `GET /api/v1/accounting/books/{id}` | `Get_NotFound_Should_Return_404` | 404, correct error code |
| `GET /api/v1/accounting/books` | `List_Should_Return_200` | 200, no params (tests binding defaults) |
| `GET /api/v1/accounting/books` | `List_WithParams_Should_Return_200` | 200, query params present |
| `POST /books/{id}/activate` | `Activate_Should_Return_200` | 200 |
| `POST /books/{id}/activate` | `Activate_NotFound_Should_Return_404` | 404 |
| `POST /books/{id}/suspend` | `Suspend_Should_Return_200` | 200 |
| `POST /books/{id}/suspend` | `Suspend_NotFound_Should_Return_404` | 404 |
| `POST /books/{id}/archive` | `Archive_Should_Return_200` | 200 |
| `POST /books/{id}/archive` | `Archive_NotFound_Should_Return_404` | 404 |

### Arranging state for smoke tests

For tests that need existing data (e.g. `Activate_Should_Return_200`), use the HTTP `POST` endpoint itself as a one-liner helper to create the required entity. This keeps the test self-contained and verifies the create path works as well.

### Example

```csharp
[Collection("AccountingBookHttpTestsCollection")]
public sealed class AccountingBookHttpTests : IAsyncLifetime
{
    private readonly AccountingBookHttpTestsFixture _fixture;
    private HttpClient _client = null!;

    public AccountingBookHttpTests(AccountingBookHttpTestsFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() =>
        _client = _fixture.Factory.CreateClient();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Get_Existing_Should_Return_200()
    {
        await ArrangeCreateBookAsync("smoke-get");

        await using var conn = _fixture.CreateAccountingConnection();
        await conn.OpenAsync();
        var id = await conn.ExecuteScalarAsync<long>("SELECT TOP 1 id FROM accounting_book ORDER BY id DESC");

        var response = await _client.GetAsync($"/api/v1/accounting/books/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// Arrange via HTTP create endpoint - keeps test self-contained.
}
```

The fixture uses `ApexWebApplicationFactory` + `ICollectionFixture` for container reuse.

### What NOT to put in HTTP tests

- Lifecycle/state transitions (activate → suspend → archive)
- Conflict/rejection scenarios
- Complex multi-entity workflows
- Transaction verification

These belong in handler integration tests.

---

## 2. Handler integration tests

Handler tests are the workhorse of Apex integration testing. They exercise real handlers against a real database with real DI, real transactions, and real migrations.

### Base class

Use:

```csharp
ApexIntegrationTestBase
```

This starts one SQL Server container.

Both logical connection strings point to the same physical database:

```text
AccountingReadDb  == AccountingWriteDb
```

### Arranging data - use real handlers

**Rule:** When setting up test data, invoke the actual handler rather than using raw SQL or HTTP. This ensures normalization, validation, and business rules are applied — and it exercises the handler as part of the arrange step, giving you a free happy-path test.

Good:

```csharp
var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
var book = await createHandler.HandleAsync(new CreateAccountingBookRequest
{
    Code = "test-001",
    Title = "Test Book",
    OwnerType = "PORTFOLIO",
    OwnerId = "100"
});
```

Bad (raw SQL bypasses normalization and validation):

```csharp
await connection.ExecuteAsync("""
    INSERT INTO accounting_book(id, code, title, ...) VALUES (...)
    """);
```

Bad (HTTP is slower and adds unnecessary transport layer):

```csharp
var response = await _client.PostAsJsonAsync("/api/v1/accounting/books", new { ... });
```

### Example

```csharp
[Collection(ApexIntegrationTestCollection.Name)]
public sealed class AccountingBookHandlerTests : ApexIntegrationTestBase
{
    public AccountingBookHandlerTests(ApexIntegrationTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Activate_DraftToActive_Should_Succeed()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        // Arrange - use real handlers
        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var book = await createHandler.HandleAsync(new CreateAccountingBookRequest
        {
            Code = "act-1",
            Title = "Test",
            OwnerType = "PORTFOLIO",
            OwnerId = "700"
        });

        // Act
        var handler = scope.Services.GetRequiredService<ActivateAccountingBookHandler>();
        var result = await handler.HandleAsync(book.Id);

        // Assert response.
        // Assert persisted state.
        Assert.Equal("ACTIVE", result.Status);
        Assert.NotNull(result.ActivatedAt);

        // Optional: SQL round-trip verification
        await using var conn = CreateAccountingConnection();
        await conn.OpenAsync();
        var dbStatus = await conn.ExecuteScalarAsync<string>(
            "SELECT status FROM accounting_book WHERE id = @Id",
            new { Id = book.Id });
        Assert.Equal("ACTIVE", dbStatus);
    }
}
```

### Testing exceptions

Handlers throw typed exceptions. Assert them directly:

```csharp
var ex = await Assert.ThrowsAsync<ConflictException>(() =>
    createHandler.HandleAsync(new CreateAccountingBookRequest { ... }));

Assert.Equal(AccountingBookErrors.AccountingBookCodeAlreadyExists, ex.ErrorCode);
```

### Complex scenarios

Handler tests are where complex workflows live. Example: full lifecycle:

```csharp
[Fact]
public async Task FullLifecycle_Should_Work()
{
    await ResetAccountingDatabaseAsync();

    await using var scope = await CreateScopeAsync();

    var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
    var activateHandler = scope.Services.GetRequiredService<ActivateAccountingBookHandler>();
    var suspendHandler = scope.Services.GetRequiredService<SuspendAccountingBookHandler>();
    var archiveHandler = scope.Services.GetRequiredService<ArchiveAccountingBookHandler>();
    var getHandler = scope.Services.GetRequiredService<GetAccountingBookHandler>();

    // Create → Activate → Suspend → Archive → Verify
    var book = await createHandler.HandleAsync(new CreateAccountingBookRequest { ... });
    await activateHandler.HandleAsync(book.Id);
    await suspendHandler.HandleAsync(book.Id);
    await archiveHandler.HandleAsync(book.Id);

    var final = await getHandler.HandleAsync(book.Id);
    Assert.Equal("ARCHIVED", final.Status);
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

| You need to test... | Use... |
|---|---|
| Endpoint binding, status codes, `ProblemDetails` shape | HTTP smoke tests |
| Handler business logic, transactions, conflicts, workflows | Handler integration tests (`ApexIntegrationTestBase`) |
| Read/write physical routing | `SeparatedReadWriteIntegrationTestBase` |
| State machine rules, value objects, pure logic | Domain unit tests (`Apex.UnitTests`) |

---

## Migrations

### Production migrations

Live under `tools/Apex.DatabaseMigrator/Scripts/Accounting/`:

```text
tools/Apex.DatabaseMigrator/Scripts/Accounting/
  000001_create_accounting_book.sql
```

Run automatically by fixtures via:

```csharp
DatabaseMigrationRunner.RunAccountingMigrations(connectionString);
```

These are the same migrations the real application uses. Never put test-only tables here.

### Test-only migrations

Test support tables (e.g. `db_marker`, `write_transaction_test`) live in a separate folder:

```text
tools/Apex.DatabaseMigrator/Scripts.Test/Accounting/
  000001_test_integration_support_tables.sql
```

Run via:

```csharp
DatabaseMigrationRunner.RunTestMigrations(connectionString);
```

Fixtures that need both call both functions:

```csharp
DatabaseMigrationRunner.RunAccountingMigrations(connectionString);
DatabaseMigrationRunner.RunTestMigrations(connectionString);
```

### Rules

- All schema must come from DbUp migrations — never create tables in test code.
- Production app only runs migrations from `Scripts/Accounting/`.
- Test migrations are strictly for tables needed by integration tests.
- If a table is needed by production code, it belongs in `Scripts/`, not `Scripts.Test/`.

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
---
```

## Test data and arrange/setup

**Arranging data via handlers — not HTTP or raw SQL.**

When a handler integration test needs an existing entity (e.g. you need a book in `DRAFT` state to test activation), invoke the creation handler itself.

Good:

```csharp
var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
var book = await createHandler.HandleAsync(new CreateAccountingBookRequest
{
    Code = "test-001",
    Title = "Test Book",
    OwnerType = "PORTFOLIO",
    OwnerId = "100"
});
```

This ensures normalization, validation, and business rules run as part of the arrange step.

Tests may also use raw SQL for asserting persisted state:

```csharp
await using var conn = CreateAccountingConnection();
await conn.OpenAsync();
var count = await conn.ExecuteScalarAsync<int>(
    "SELECT COUNT(*) FROM accounting_book WHERE id = @Id",
    new { Id = book.Id });
Assert.Equal(1, count);
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
    ApexWebApplicationFactory.cs

  Accounting/
    AccountingSingleDatabaseTests.cs              # infrastructure single-DB tests
    AccountingBooks/
      AccountingBookHttpTests.cs                  # HTTP smoke (binding, status codes)
      AccountingBookHandlerTests.cs               # handler integration (lifecycle, conflicts, workflows)

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

## Checklist for a new HTTP smoke test

1. Put the test under `tests/Apex.IntegrationTests/` (e.g. `Accounting/AccountingBooks/`).
2. Use `WebApplicationFactory` + `ICollectionFixture` for container reuse.
3. One test per endpoint (success), plus 404 test for ID-based endpoints.
4. Assert status code and deserializable body. For 404, assert `ProblemDetails.ErrorCode`.
5. Arrange data via HTTP create endpoint (self-contained).
6. No lifecycle/state transitions — those belong in handler tests.

---

## Checklist for a new handler integration test

1. Put the test under `tests/Apex.IntegrationTests/Accounting/` (matching module structure).
2. Inherit from `ApexIntegrationTestBase`.
3. Call `await ResetAccountingDatabaseAsync();` when the test mutates data.
4. Resolve scoped services from `await CreateScopeAsync();`.
5. Arrange data by invoking the real creation handler — not SQL or HTTP.
6. Act: call the handler under test.
7. Assert: response object, then optional SQL round-trip verification.
8. For exceptions: assert typed exception and `ErrorCode`.
9. Make sure all required schema exists through DbUp migrations.
10. Keep the test deterministic.

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

1. **HTTP tests are thin:** one test per endpoint, success + 404. Nothing else.
2. **Handler tests are the workhorse:** all business logic, lifecycle, conflicts, and workflows live here.
3. **Domain tests for pure logic:** state machine rules, value objects, no DB needed.
4. **Arrange with real handlers:** never use raw SQL or HTTP to set up data in handler tests.
5. **Production migrations stay pure:** test-only tables go in `Scripts.Test/`, production in `Scripts/`.
6. **Resolve scoped services from `CreateScopeAsync()`:** never from the root provider.
7. **Reset mutated data at the beginning of each test:** use `ResetAccountingDatabaseAsync()`.
8. **Use direct SQL only for asserting persisted state:** not for business behavior.
9. **Keep tests deterministic and isolated.**
