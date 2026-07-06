# Apex Implementation Handbook

This handbook captures the conventions and decisions established through the AccountingBooks implementation. Use it as your primary reference when starting a new capability.

---

## 1. Capability checklist

When adding a new capability to an existing module, follow this order:

1. Write the DbUp migration (`00000N_create_<table>.sql`).
2. Create `Domain/` — entity, status enum, error codes.
3. Create `SqlModels/` — flat POCO matching the table.
4. Create `Repositories/` — read and write repos, no interfaces.
5. Create `UseCases/` — request, response, validator, handler per use case.
6. Add capability endpoint mapper at the capability root.
7. Wire endpoint mapper into the module's top-level endpoint mapper.
8. Register repos, handlers, validators in the module's `DependencyInjection.cs`.
9. Add HTTP integration tests.
10. Add unit tests for domain rules and validators.

---

## 2. Domain model

The domain model owns state-transition rules. It must not depend on Dapper, ASP.NET Core, or infrastructure types.

```csharp
public sealed class AccountingBook
{
    public long Id { get; init; }
    public string Code { get; private set; } = null!;
    public AccountingBookStatus Status { get; private set; }

    internal AccountingBook() { }

    public static AccountingBook Create(long id, string code, ...)
    {
        // validate, normalize, return new instance
    }

    public void Activate(DateTime now)
    {
        if (Status != AccountingBookStatus.Draft && Status != AccountingBookStatus.Suspended)
            throw new BusinessRuleException("...", AccountingBookErrors.AccountingBookCannotBeActivated);

        Status = AccountingBookStatus.Active;
    }
}
```

Rules:

| Rule | Reason |
|---|---|
| Properties use `private set` | Enforcement of encapsulation |
| `internal AccountingBook()` | Required for object initializer in factory methods |
| State transitions are public methods | Domain behavior is part of the public API |
| Validation throws `BusinessRuleException` | Handlers let exceptions bubble; middleware maps them |

### Status patterns

Use an enum in C#, string in the database:

```csharp
public enum AccountingBookStatus { Draft, Active, Suspended, Archived }

public static class AccountingBookStatusExtensions
{
    public static string ToDatabaseValue(this AccountingBookStatus status) =>
        status switch {
            AccountingBookStatus.Draft => "DRAFT",
            AccountingBookStatus.Active => "ACTIVE",
            // ...
        };

    public static AccountingBookStatus FromDatabaseValue(string value) =>
        value switch {
            "ACTIVE" => AccountingBookStatus.Active,
            // ...
        };
}
```

### Error codes

Put them in a static class, snake_case const strings:

```csharp
public static class AccountingBookErrors
{
    public const string AccountingBookNotFound = "accounting_book_not_found";
    public const string AccountingBookCodeAlreadyExists = "accounting_book_code_already_exists";
    public const string AccountingBookCannotBeActivated = "accounting_book_cannot_be_activated";
}
```

Use `NotFoundException` for missing entities, `ConflictException` for uniqueness collisions, and `BusinessRuleException` for domain rule violations.

---

## 3. SQL model

The SqlModel is a flat POCO that mirrors the database table row-for-row. It exists solely for Dapper hydration.

```csharp
public sealed class AccountingBookSqlModel
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public string Status { get; set; } = null!;
    // ... matches all DB columns
}
```

Decisions:

| Decision | Rationale |
|---|---|
| `Status` is `string`, not enum | DB speaks strings. No implicit enum mapping. |
| No `ISqlModel` interface | Marker interface buys nothing. Naming convention is enough. |
| `MapToDomain()` on SqlModel | Collocates the mapping. Single authority. |
| No `ToSqlModel()` on domain | No consumer. The write repo extracts what it needs inline. |

### SqlModel.MapToDomain()

The SqlModel owns the mapping to the domain entity:

```csharp
public AccountingBook MapToDomain()
{
    return AccountingBook.CreateFromSql(this);
}
```

The domain class receives the SqlModel through an internal factory:

```csharp
internal static AccountingBook CreateFromSql(AccountingBookSqlModel model)
{
    return new AccountingBook
    {
        Id = model.Id,
        Code = model.Code,
        Status = AccountingBookStatusExtensions.FromDatabaseValue(model.Status),
        // ...
    };
}
```

No Mapster. No generic interfaces. Manual mapping is explicit and debuggable.

---

## 4. Repositories

No repository interfaces. Direct classes only.

### Read repository

- Depends on `IReadDbConnectionFactory`
- Opens its own connection per method (`await using var connection = ...`)
- Returns `null` for queries that find nothing (not `NotFoundException`)
- Returns SqlModel or maps to response in the handler

```csharp
public sealed class AccountingBookReadRepository
{
    private readonly IReadDbConnectionFactory _connectionFactory;

    public AccountingBookReadRepository(IReadDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<AccountingBookSqlModel?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(AccountingModule.Name, ct);
        return await connection.QuerySingleOrDefaultAsync<AccountingBookSqlModel>(
            new CommandDefinition("""
                SELECT id AS Id, code AS Code, ...
                FROM accounting_book
                WHERE id = @Id
                """, new { Id = id }, cancellationToken: ct));
    }
}
```

### Write repository

- Depends on `IWriteDbSession`
- Never opens its own connection — uses `_session.Connection` and `_session.Transaction`
- Uses `WITH (UPDLOCK, ROWLOCK)` for `GetByIdForUpdateAsync`
- Maps SqlModel to domain using `model.MapToDomain()`

```csharp
public sealed class AccountingBookWriteRepository
{
    private readonly IWriteDbSession _session;

    public AccountingBookWriteRepository(IWriteDbSession session) =>
        _session = session;

    public async Task<AccountingBook?> GetByIdForUpdateAsync(long id, CancellationToken ct = default)
    {
        var model = await _session.Connection.QuerySingleOrDefaultAsync<AccountingBookSqlModel>(
            new CommandDefinition("""
                SELECT id AS Id, ...
                FROM accounting_book WITH (UPDLOCK, ROWLOCK)
                WHERE id = @Id
                """, new { Id = id }, transaction: _session.Transaction, cancellationToken: ct));

        return model == null ? null : model.MapToDomain();
    }
}
```

---

## 5. Use cases (handlers)

All use cases go in the `UseCases/` subfolder inside the capability.

### Named handler pattern

Use `*Handler.cs` naming. Not `*UseCase.cs`. Consistent with the module architecture.

### Command handler template

```csharp
public sealed class ActivateAccountingBookHandler
{
    private readonly IWriteTransactionRunner _transactionRunner;
    private readonly AccountingBookWriteRepository _writeRepository;
    private readonly IClock _clock;

    public ActivateAccountingBookHandler(...) { }

    public async Task<ActivateAccountingBookResponse> HandleAsync(long id, CancellationToken ct = default)
    {
        ActivateAccountingBookResponse? response = null;

        await _transactionRunner.ExecuteAsync(AccountingModule.Name, async innerCt =>
        {
            var book = await _writeRepository.GetByIdForUpdateAsync(id, innerCt);

            if (book == null)
                throw new NotFoundException($"...", AccountingBookErrors.AccountingBookNotFound);

            book.Activate(_clock.UtcNow);
            await _writeRepository.UpdateStatusAsync(book, innerCt);

            response = new ActivateAccountingBookResponse(book.Id, book.Code, ...);
        }, ct);

        return response!;
    }
}
```

Key patterns:

| Pattern | Why |
|---|---|
| `response!` null-forgiving | Compiler doesn't see closure assignment. Safe because transaction always runs. |
| `BookNotFound` check throws `NotFoundException` | Centralized middleware will convert to 404 ProblemDetails. |
| State mutation calls domain method `book.Activate()` | Domain owns the rule. Handler orchestrates. |
| Consistency checks use write repo inside transaction | Read repo is on a different connection; would miss uncommitted writes. |

### Query handler template

```csharp
public sealed class GetAccountingBookHandler
{
    private readonly AccountingBookReadRepository _readRepository;

    public async Task<GetAccountingBookResponse> HandleAsync(long id, CancellationToken ct = default)
    {
        var model = await _readRepository.GetByIdAsync(id, ct);

        if (model == null)
            throw new NotFoundException($"...", AccountingBookErrors.AccountingBookNotFound);

        return MapResponse(model);
    }
}
```

Query handlers never use `IWriteTransactionRunner`, `IWriteDbSession`, or write repositories.

---

## 6. Endpoints

Endpoints are thin. They map HTTP to handlers and return results.

```csharp
public static class AccountingBookEndpoints
{
    public static IEndpointRouteBuilder MapAccountingBookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/apex/v1/api/accounting/books")
            .WithTags("Accounting - Books");

        group.MapPost("/", async (CreateAccountingBookRequest request,
                                   CreateAccountingBookHandler handler,
                                   CancellationToken ct) =>
        {
            var response = await handler.HandleAsync(request, ct);
            return Results.Created($"/apex/v1/api/accounting/books/{response.Id}", response);
        });

        group.MapPost("/{id:long}/activate", async (long id,
                                                     ActivateAccountingBookHandler handler,
                                                     CancellationToken ct) =>
        {
            var response = await handler.HandleAsync(id, ct);
            return Results.Ok(response);
        });

        return app;
    }
}
```

Wire the capability endpoint in the module's top-level endpoint mapper:

```csharp
// AccountingEndpoints.cs
using Apex.Modules.Accounting.AccountingBooks.Endpoints;

app.MapAccountingBookEndpoints();
```

---

## 7. DI registration

Register in the module's `DependencyInjection.cs`:

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddAccountingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<AccountingBookReadRepository>();
        services.AddScoped<AccountingBookWriteRepository>();

        services.AddTransient<CreateAccountingBookHandler>();
        services.AddTransient<ActivateAccountingBookHandler>();
        services.AddTransient<GetAccountingBookHandler>();

        services.AddTransient<IValidator<CreateAccountingBookRequest>, CreateAccountingBookValidator>();

        return services;
    }
}
```

Scoped for repos (lifetimes with DI-managed sessions), transient for handlers and validators.

---

## 8. Migration conventions

- File lives in `tools/Apex.DatabaseMigrator/Scripts/<ModuleName>/`.
- 6-digit zero-padded prefix: `000003_create_accounting_book.sql`.
- Module-prefixed table names: `accounting_book`, not `book`.
- `snake_case` column names.
- `BIGINT` for TSID IDs. No `IDENTITY`.
- Constraints: unique, check, foreign key — in the migration, not in code.
- Explicit index names: `ux_accounting_book_code`, `ix_accounting_book_status`.

---

## 9. Integration testing — HTTP only

Integration tests go through HTTP using `WebApplicationFactory<Program>`. No handler-direct tests.

Why HTTP only:

| Concern | Covered by HTTP test? |
|---|---|
| Model binding | Yes |
| Validation → 400 shape | Yes |
| Business rule → 422 + ProblemDetails | Yes |
| SQL + transaction behavior | Yes |
| `traceId` and `errorCode` in response | Yes |
| Content-Type `application/problem+json` | Yes |

### Test fixture

Each capability family gets its own fixture and collection:

```csharp
// Fixture owns the WebApplicationFactory and SQL container
public class AccountingBookHttpTestsFixture : IAsyncLifetime
{
    private readonly ApexWebApplicationFactory _factory = new();

    public ApexWebApplicationFactory Factory => _factory;

    public async Task InitializeAsync() => await _factory.InitializeAsync();
    public async Task DisposeAsync() => await _factory.DisposeAsync();
    public Task ResetAccountingDatabaseAsync() => _factory.ResetAccountingDatabaseAsync();
}

[CollectionDefinition("AccountingBookHttpTestsCollection")]
public class AccountingBookHttpTestsCollection
    : ICollectionFixture<AccountingBookHttpTestsFixture> { }
```

### Test class template

```csharp
[Collection("AccountingBookHttpTestsCollection")]
public sealed class AccountingBookHttpTests : IAsyncLifetime
{
    private HttpClient _client = null!;
    private readonly AccountingBookHttpTestsFixture _fixture;

    public AccountingBookHttpTests(AccountingBookHttpTestsFixture fixture) =>
        _fixture = fixture;

    public async Task InitializeAsync() => _client = _fixture.Factory.CreateClient();
    public async Task DisposeAsync() => _client.Dispose();

    [Fact]
    public async Task Create_Should_CreateDraftAccountingBook()
    {
        await _fixture.ResetAccountingDatabaseAsync();

        var response = await _client.PostAsJsonAsync("/apex/v1/api/accounting/books",
            new CreateAccountingBookRequest { Code = "test", Title = "T", OwnerType = "P", OwnerId = "1" });

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateAccountingBookResponse>(JsonOptions);
        Assert.Equal("DRAFT", body.Status);
    }

    [Fact]
    public async Task Create_Should_RejectDuplicateCode()
    {
        await _fixture.ResetAccountingDatabaseAsync();
        await _client.PostAsJsonAsync("/apex/v1/api/accounting/books",
            new CreateAccountingBookRequest { Code = "dup", Title = "T", OwnerType = "P", OwnerId = "1" });

        var response = await _client.PostAsJsonAsync("/apex/v1/api/accounting/books",
            new CreateAccountingBookRequest { Code = "dup", Title = "T2", OwnerType = "P", OwnerId = "2" });

        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.Equal("accounting_book_code_already_exists", problem.ErrorCode);
        Assert.NotEmpty(problem.TraceId);
    }
}
```

### What to assert

| Error type | HTTP status | `errorCode` | Additional |
|---|---|---|---|
| Validation | 400 | `validation_failed` | `errors` dict present |
| Not found | 404 | `<resource>_not_found` | `traceId` present |
| Conflict | 409 | `<resource>_already_exists` | `traceId` present |
| Business rule | 422 | `<resource>_cannot_be_<action>` | `traceId` present |

### Unit tests still apply

Unit tests remain for:

- Domain state-transition rules
- Validator behavior
- Pure mapping logic

Use `xUnit` + `NSubstitute` where mocking makes sense. Do not mock Dapper or `SqlConnection`.

---

## 10. Anti-patterns

| Anti-pattern | Why | Fix |
|---|---|---|
| SQL in handlers | Breaks persistence boundary | SQL lives in repos only |
| `try/catch` in endpoints | Middleware owns error mapping | Let exceptions bubble |
| Interface for every repo | Unnecessary abstraction | Direct repo classes |
| `ToSqlModel()` on domain entity | No consumer, dead code | Write repo extracts what it needs |
| Mapster for simple mapping | Hidden magic, harder to debug | Manual `MapToDomain()` and static mappers |
| `ISqlModel` marker interface | No behavioral contract | Naming convention is the guard |
| Handler-direct integration tests | Miss model binding, middleware, ProblemDetails | HTTP-only through `WebApplicationFactory` |
| `SELECT *` in SQL | Fragile to schema changes | Explicit column lists with `AS` aliasing |
| `Identity` columns for IDs | TSID is the strategy | `BIGINT` not `IDENTITY` |
| `DateTime.UtcNow` directly | Not testable | Use `IClock.UtcNow` |
| Raw string in `_transactionRunner.ExecuteAsync("Accounting", ...)` | Copy-paste risk | Use `AccountingModule.Name` |

---

## 11. Reference links

The handbook is a living summary. The source-of-truth guides follow:

| Concern | Guide |
|---|---|
| Module and capability structure | `docs/module-architecture-guide.md` |
| Exception hierarchy, ProblemDetails, error codes | `docs/exception-handling-guide.md` |
| Dapper, transactions, DbUp, sharding, connection pooling | `docs/database-persistence-guide.md` |
| Testcontainers, collection fixtures, separated read/write | `docs/integration-testing-guide.md` |
