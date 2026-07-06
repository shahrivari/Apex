# Apex Module Architecture Guide

This guide defines how to create and organize modules in Apex.

Apex is a modular monolith. Each module owns its own business capabilities, domain concepts, use cases, endpoints, persistence code, and dependency registration.

The goal is to keep modules independent, understandable, testable, and ready to be extracted later if needed.

---

## 1. Module principle

A module is a business boundary.

Examples:

```text
Apex.Modules.Accounting
Apex.Modules.Assets
Apex.Modules.IdentityAccess
Apex.Modules.ReferenceData
```

A module should not be a technical layer.

Good module names:

```text
Accounting
Assets
IdentityAccess
ReferenceData
Portfolio
Payments
```

Bad module names:

```text
Services
Repositories
Controllers
Infrastructure
Common
```

---

## 2. Top-level module structure

Every module should start with this shape:

```text
Apex.Modules.<ModuleName>/
  <ModuleName>Module.cs
  DependencyInjection.cs

  Endpoints/
    <ModuleName>Endpoints.cs

  <CapabilityA>/
  <CapabilityB>/
  <CapabilityC>/
```

Example:

```text
Apex.Modules.Accounting/
  AccountingModule.cs
  DependencyInjection.cs

  Endpoints/
    AccountingEndpoints.cs

  FiscalYears/
  ChartOfAccounts/
  Journals/
```

---

## 3. Module marker file

Each module has a marker/static metadata file.

Example:

```csharp
namespace Apex.Modules.Accounting;

public static class AccountingModule
{
    public const string Name = "Accounting";
}
```

Use this constant for:

```csharp
IReadDbConnectionFactory.OpenConnectionAsync(AccountingModule.Name, cancellationToken);
IWriteTransactionRunner.ExecuteAsync(AccountingModule.Name, ...);
```

Do not repeat module names as string literals everywhere.

---

## 4. Module dependency registration

Each module owns its own DI registration.

Example:

```csharp
namespace Apex.Modules.Accounting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register module repositories, handlers, validators, services.

        return services;
    }
}
```

The API project should call:

```csharp
builder.Services.AddAccountingModule(builder.Configuration);
```

The module should not force other modules to register its internals manually.

---

## 5. Module endpoint registration

Each module has one top-level endpoint mapper.

Example:

```csharp
namespace Apex.Modules.Accounting.Endpoints;

using Apex.Modules.Accounting.FiscalYears;
using Microsoft.AspNetCore.Routing;

public static class AccountingEndpoints
{
    public static IEndpointRouteBuilder MapAccountingEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapFiscalYearEndpoints();
        app.MapChartOfAccountEndpoints();
        app.MapJournalEndpoints();

        return app;
    }
}
```

The API project should call only the module endpoint mapper:

```csharp
app.MapAccountingEndpoints();
```

Do not map every capability directly from `Program.cs`.

---

## 6. Capability-first organization

Inside a module, organize by business capability first.

Example:

```text
Accounting/
  FiscalYears/
  ChartOfAccounts/
  Journals/
```

Not like this:

```text
Accounting/
  Controllers/
  Services/
  Repositories/
  Dtos/
  Models/
```

Capability-first structure keeps related code together and makes business boundaries obvious.

---

## 7. Standard capability structure

A capability should use this structure by default:

Use-case folders are grouped under a `UseCases/` subfolder. This keeps the
capability root organized and separates concerns:

- `Domain/`, `Repositories/`, `SqlModels/` are static concern folders
- `UseCases/` groups all use-case folders together
- The capability endpoint mapper stays at the capability root

```text
<CapabilityName>/
  <CapabilityName>Endpoints.cs

  Domain/
    <Entity>.cs
    <Enum>.cs
    <ValueObject>.cs

  Repositories/
    <CapabilityName>ReadRepository.cs
    <CapabilityName>WriteRepository.cs

  UseCases/
    <UseCaseA>/
      <UseCaseA>Request.cs
      <UseCaseA>Response.cs
      <UseCaseA>Validator.cs
      <UseCaseA>Handler.cs

    <UseCaseB>/
      <UseCaseB>Response.cs
      <UseCaseB>Handler.cs
```

Example:

```text
AccountingBooks/
  AccountingBookEndpoints.cs

  Domain/
    AccountingBook.cs
    AccountingBookStatus.cs
    AccountingBookErrors.cs

  SqlModels/
    AccountingBookSqlModel.cs

  Repositories/
    AccountingBookReadRepository.cs
    AccountingBookWriteRepository.cs

  UseCases/
    CreateAccountingBook/
      CreateAccountingBookRequest.cs
      CreateAccountingBookResponse.cs
      CreateAccountingBookValidator.cs
      CreateAccountingBookHandler.cs

    GetAccountingBook/
      GetAccountingBookResponse.cs
      GetAccountingBookHandler.cs

    ListAccountingBooks/
      ListAccountingBooksRequest.cs
      ListAccountingBooksResponse.cs
      ListAccountingBooksHandler.cs

    ActivateAccountingBook/
      ActivateAccountingBookRequest.cs
      ActivateAccountingBookResponse.cs
      ActivateAccountingBookHandler.cs

    SuspendAccountingBook/
      SuspendAccountingBookRequest.cs
      SuspendAccountingBookResponse.cs
      SuspendAccountingBookHandler.cs

    ArchiveAccountingBook/
      ArchiveAccountingBookResponse.cs
      ArchiveAccountingBookHandler.cs
```

---

## 8. Use-case folder rule

Each meaningful use case gets its own folder.

Good:

```text
CreateFiscalYear/
OpenFiscalYear/
CloseFiscalYear/
GetFiscalYear/
ListFiscalYears/
```

Bad:

```text
Commands/
Queries/
Services/
Managers/
```

The folder name should describe a user/business action.

---

## 9. What belongs in a use-case folder

A command use case usually contains:

```text
<CreateX>Request.cs
<CreateX>Response.cs
<CreateX>Validator.cs
<CreateX>Handler.cs
```

A query use case usually contains:

```text
<GetX>Response.cs
<GetX>Handler.cs
```

If a query has filters/paging, add:

```text
<ListX>Request.cs
```

Avoid generic names like:

```text
Request.cs
Response.cs
Handler.cs
Validator.cs
```

Use full names:

```text
CreateFiscalYearRequest.cs
CreateFiscalYearHandler.cs
```

This makes search/navigation easier.

---

## 10. Domain folder

The `Domain/` folder contains business concepts owned by the capability.

Use it for:

- entities
- value objects
- enums
- domain-specific rules
- small domain methods

Example:

```text
Domain/
  FiscalYear.cs
  FiscalYearStatus.cs
```

Domain classes should not depend on:

```text
Dapper
ASP.NET Core
IConfiguration
IServiceProvider
DbConnection
HttpContext
```

Domain should stay clean.

---

## 11. Repository separation

Every capability should separate read and write persistence.

```text
Repositories/
  <CapabilityName>ReadRepository.cs
  <CapabilityName>WriteRepository.cs
```

Example:

```text
Repositories/
  FiscalYearReadRepository.cs
  FiscalYearWriteRepository.cs
```

---

## 12. Read repository rule

Read repositories use:

```csharp
IReadDbConnectionFactory
```

They are used by query handlers.

They should contain read/query methods only:

```text
GetByIdAsync
ListAsync
SearchAsync
ExistsAsync
GetCurrentAsync
```

They should not depend on:

```csharp
IWriteDbSession
IWriteTransactionRunner
```

They should not mutate state.

---

## 13. Write repository rule

Write repositories use:

```csharp
IWriteDbSession
```

They are used by command handlers.

They should contain:

```text
InsertAsync
UpdateAsync
DeleteAsync
OpenAsync
CloseAsync
GetByIdForUpdateAsync
ExistsForUpdateAsync
```

They should be called inside:

```csharp
IWriteTransactionRunner
```

They should use:

```csharp
_session.Connection
_session.Transaction
```

Write repositories should not open their own write connection.

---

## 14. Command handler rule

Command handlers mutate state.

They should use:

```csharp
IWriteTransactionRunner
<Capability>WriteRepository
IValidator<TRequest>
IIdGenerator
IClock
```

A command handler should generally follow this flow:

```text
1. Validate request
2. Start write transaction
3. Run command-side consistency checks
4. Create/update domain object
5. Persist using write repository
6. Return response
```

Command-side consistency checks must use the write repository when correctness depends on transactionally consistent data.

Example:

```text
Checking duplicate account code
Checking overlapping fiscal year
Checking journal balance before posting
```

Do not use the read repository for these checks unless eventual consistency is acceptable.

---

## 15. Query handler rule

Query handlers read state.

They should use:

```csharp
<Capability>ReadRepository
```

They should not use:

```csharp
IWriteTransactionRunner
IWriteDbSession
<Capability>WriteRepository
```

A query handler should generally:

```text
1. Accept query parameters
2. Call read repository
3. Map result to response
4. Return response
```

---

## 16. Endpoint rule

Capability endpoints should only handle HTTP concerns.

Endpoint files may:

- define route group
- map route methods
- bind request body/query/path
- call handler
- return HTTP result

Endpoint files must not contain:

- SQL
- business logic
- transaction logic
- validation logic beyond route constraints

Example:

```csharp
public static IEndpointRouteBuilder MapFiscalYearEndpoints(
    this IEndpointRouteBuilder app)
{
    var group = app.MapGroup("/apex/v1/api/accounting/fiscal-years")
        .WithTags("Accounting - Fiscal Years");

    group.MapPost("/", async (
        CreateFiscalYearRequest request,
        CreateFiscalYearHandler handler,
        CancellationToken cancellationToken) =>
    {
        var response = await handler.HandleAsync(request, cancellationToken);
        return Results.Created($"/apex/v1/api/accounting/fiscal-years/{response.Id}", response);
    });

    return app;
}
```

---

## 17. Request and response rule

Requests and responses belong to the use case that owns them.

Example:

```text
CreateFiscalYear/
  CreateFiscalYearRequest.cs
  CreateFiscalYearResponse.cs
```

Avoid shared DTO folders:

```text
Dtos/
Requests/
Responses/
```

Only create shared response models when multiple use cases truly return the same shape.

---

## 18. Validation rule

Use FluentValidation for request validation.

Validators should validate input shape and simple input rules.

Examples:

```text
required fields
max length
date ranges
positive amounts
non-empty lines
```

Validators should not perform database checks.

Database-backed consistency checks belong in handlers through repositories.

---

## 19. SQL rule

SQL belongs in repositories.

Do not put SQL in:

```text
Endpoints
Handlers
Validators
Domain objects
```

Repositories are the persistence boundary.

---

## 20. Transactions rule

Only command handlers should start write transactions.

Use:

```csharp
IWriteTransactionRunner.ExecuteAsync(<ModuleName>Module.Name, async ct =>
{
    // write repository calls
}, cancellationToken);
```

Write repository methods assume an active write session when they mutate data.

Do not start transactions in repositories.

---

## 21. DbUp migration rule

Schema belongs in DbUp migrations.

Do not create schema from code or tests.

Migration files live in:

```text
tools/Apex.DatabaseMigrator/Scripts/<ModuleName>/
```

Example:

```text
tools/Apex.DatabaseMigrator/Scripts/Accounting/
  000001_create_accounting_fiscal_years.sql
  000002_create_accounting_chart_of_accounts.sql
  000003_create_accounting_journal_entries.sql
```

Use one migration file per meaningful schema change.

---

## 22. Tests rule

Use unit tests for:

```text
domain rules
validators
small handlers with mocks
pure logic
```

Use integration tests for:

```text
Dapper SQL
DbUp migrations
repositories
transaction commit/rollback
read/write connection behavior
feature behavior with real database
```

Business integration tests should inherit from:

```csharp
ApexIntegrationTestBase
```

Read/write physical routing tests should inherit from:

```csharp
SeparatedReadWriteIntegrationTestBase
```

---

## 23. Cross-module communication

Modules should not directly use another module's repositories or tables.

Bad:

```csharp
// Assets module directly calls Accounting repository.
AccountingJournalEntryWriteRepository
```

Good:

```text
Accounting exposes a contract/service.
Other modules call the contract.
Accounting owns its persistence.
```

Use a `Contracts/` folder when a module needs to expose a stable internal API.

Example:

```text
Apex.Modules.Accounting/
  Contracts/
    IAccountingDocumentService.cs
    CreateAccountingDocumentCommand.cs
    CreateAccountingDocumentResult.cs
```

Other modules may depend on contracts, not internal repositories.

---

## 24. When to create a Contracts folder

Create `Contracts/` when another module needs to call this module.

Example:

```text
Payments module needs to create accounting documents.
Assets module needs to post accounting journal entries.
```

Do not expose repositories as contracts.

Contracts should be use-case oriented.

Good:

```text
CreateAccountingDocumentAsync
PostJournalEntryAsync
GetAccountSnapshotAsync
```

Bad:

```text
InsertJournalEntryRowAsync
UpdateAccountTableAsync
```

---

## 25. Dependency direction

Recommended dependency direction:

```text
Apex.Api
  -> Apex.Infrastructure
  -> Apex.Application
  -> Apex.Modules.*

Apex.Modules.*
  -> Apex.Application
```

Modules can use application abstractions:

```text
IReadDbConnectionFactory
IWriteDbSession
IWriteTransactionRunner
IIdGenerator
IClock
```

Modules should not depend on `Apex.Api`.

Avoid direct dependency from one module to another unless using explicit contracts.

---

## 26. Naming conventions

Use full use-case names.

Good:

```text
CreateFiscalYearHandler
CreateFiscalYearRequest
CreateFiscalYearValidator
FiscalYearReadRepository
FiscalYearWriteRepository
```

Bad:

```text
CreateHandler
Request
Validator
Repository
Service
Manager
```

---

## 27. When to split files more

Start with the standard structure.

Split further only when needed.

If endpoint file becomes large:

```text
FiscalYears/
  Endpoints/
    CreateFiscalYearEndpoint.cs
    GetFiscalYearEndpoint.cs
    ListFiscalYearsEndpoint.cs
```

If repository becomes large:

```text
Repositories/
  FiscalYearReadRepository.cs
  FiscalYearWriteRepository.cs
  FiscalYearPeriodReadRepository.cs
  FiscalYearPeriodWriteRepository.cs
```

If domain becomes rich:

```text
Domain/
  FiscalYear.cs
  FiscalYearPeriod.cs
  FiscalYearPolicy.cs
  FiscalYearStatus.cs
```

Do not split early only for purity.

---

## 28. New module checklist

When creating a new module:

1. Create project: `src/Apex.Modules.<ModuleName>`.
2. Add reference to `Apex.Application`.
3. Add module marker file: `<ModuleName>Module.cs`.
4. Add module DI file: `DependencyInjection.cs`.
5. Add top-level endpoint mapper: `Endpoints/<ModuleName>Endpoints.cs`.
6. Register module in `Apex.Api/Program.cs`.
7. Map module endpoints in `Apex.Api/Program.cs`.
8. Add module database configuration under `Modules:<ModuleName>:Database`.
9. Add connection strings.
10. Add DbUp migration folder: `tools/Apex.DatabaseMigrator/Scripts/<ModuleName>/`.
11. Add first capability folder.
12. Add capability endpoint mapper.
13. Add `Domain/`.
14. Add `Repositories/`.
15. Add first command/query use case.
16. Add unit tests and integration tests.

---

## 29. New capability checklist

When creating a new capability inside a module:

1. Create folder: `<CapabilityName>/`.
2. Create endpoint mapper: `<CapabilityName>Endpoints.cs`.
3. Create `Domain/`.
4. Create `Repositories/`.
5. Create read repository.
6. Create write repository.
7. Create use-case folders.
8. Register handlers/repositories/validators in module DI.
9. Map capability endpoint from module endpoint mapper.
10. Add DbUp migrations.
11. Add tests.

---

## 30. Final rules

1. Modules are business boundaries.
2. Capabilities are business sub-boundaries.
3. Use cases live inside capabilities.
4. Domain goes in `Domain/`.
5. SQL goes in `Repositories/`.
6. Read and write repositories are separate.
7. Commands use write repository and write transaction runner.
8. Queries use read repository.
9. Endpoints contain HTTP mapping only.
10. Validators validate input, not database state.
11. Migrations own schema.
12. Tests should use the correct integration-test base.
13. Avoid generic layer folders at module root.
14. Split more only when complexity requires it.
