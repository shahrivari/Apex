# Apex Architecture Guide

_A human-readable overview of how Apex is structured, why it is structured that way, and where new code belongs._

## 1. What Apex Is

Apex is a **modular monolith**.

That means the system is deployed as one application, but its code is organized into clear business modules. Each module owns a business area, such as Accounting, Assets, Portfolio, Payments, Reference Data, or Identity Access.

A module is not just a folder. It is a boundary. It owns its domain rules, use cases, endpoints, persistence code, database configuration, migrations, and tests.

The goal is to get most of the benefits of microservice-style separation without paying the operational cost of distributed services too early.

## 2. Core Architecture Principles

Apex follows a small set of rules consistently:

- Organize code by **business capability**, not by technical layer.
- Keep modules independent from each other.
- Keep HTTP, business logic, persistence, and error handling separate.
- Use Dapper directly; SQL belongs in repositories only.
- Separate read and write access.
- Use explicit transactions for commands.
- Let centralized middleware handle application errors.
- Prove business behavior mostly through handler integration tests.

The most important separation is this:

```text
Endpoints    -> HTTP mapping only
Handlers     -> use-case orchestration
Domain       -> business rules and state transitions
Repositories -> SQL and persistence
Middleware   -> error-to-HTTP mapping
Migrations   -> database schema
Tests        -> confidence at the right level
```

## 3. The Shape of a Module

A module is a business boundary. Good module names describe real business areas:

```text
Accounting
Assets
Portfolio
Payments
ReferenceData
IdentityAccess
```

Avoid technical module names such as:

```text
Services
Repositories
Infrastructure
Common
Controllers
```

A typical module has this shape:

```text
Apex.Modules.<ModuleName>/
  <ModuleName>Module.cs
  DependencyInjection.cs
  Endpoints/<ModuleName>Endpoints.cs
  <CapabilityA>/
  <CapabilityB>/
```

The module class exposes the module name constant:

```csharp
public static class AccountingModule
{
    public const string Name = "Accounting";
}
```

Use that constant everywhere the module name is needed: database factories, transaction runner, shard resolver, logging, and module registration. Avoid hardcoded module-name strings.

## 4. Capabilities

Inside a module, code is grouped by capability.

A capability is a business feature or concept, such as:

```text
AccountingBooks
FiscalYears
ChartOfAccounts
JournalEntries
```

A capability normally contains:

```text
<CapabilityName>/
  <CapabilityName>Endpoints.cs
  Domain/
  SqlModels/
  Repositories/
  UseCases/
```

A more complete example:

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

    ActivateAccountingBook/
      ActivateAccountingBookRequest.cs
      ActivateAccountingBookHandler.cs
```

This layout keeps related code close together. A developer changing Accounting Books should not need to jump across global `Controllers`, `Services`, `Repositories`, and `Models` folders.

## 5. Dependency Direction

Dependencies move inward from delivery code toward business logic and persistence abstractions.

```text
API startup
  -> Module endpoint mapper
    -> Capability endpoint mapper
      -> Use case handler
        -> Domain + repositories
          -> SQL models + Dapper
```

Rules:

- Endpoints may call handlers.
- Handlers may call repositories and domain objects.
- Repositories may use Dapper and SQL models.
- Domain may contain business behavior and may narrowly depend on module-local SQL models for rehydration.
- Domain must not depend on ASP.NET Core, Dapper, repositories, DI, or infrastructure services.
- Handlers must not contain SQL.
- Repositories must not know HTTP status codes.
- Endpoints must not contain business rules.

## 6. Endpoint Architecture

The API startup registers modules, not every internal capability.

```text
Program.cs
  -> AddAccountingModule(...)
  -> MapAccountingEndpoints()
```

The module endpoint mapper owns the top-level route group:

```text
/api/v1/accounting
```

Capability endpoint mappers attach routes under that module group:

```text
/api/v1/accounting/books
/api/v1/accounting/fiscal-years
/api/v1/accounting/journal-entries
```

This keeps routing consistent and makes the module boundary visible in the HTTP API.

Endpoints should do five things only:

1. Define the route.
2. Bind route/query/body inputs.
3. Call the correct handler.
4. Return the success result.
5. Attach metadata such as tags, authorization, and OpenAPI information.

Endpoints should not:

- contain SQL;
- contain business rules;
- start transactions;
- catch normal business exceptions;
- manually build standard error responses.

Expected business errors should flow to centralized exception middleware.

## 7. Use Case Architecture

Each use case gets its own handler. The handler is the application-level orchestrator.

Examples:

```text
CreateAccountingBookHandler
GetAccountingBookHandler
ActivateAccountingBookHandler
ArchiveAccountingBookHandler
```

A command handler typically does this:

```text
validate request
  -> generate ID
  -> get IClock.UtcNow
  -> start write transaction
  -> perform write-side consistency checks
  -> create or mutate domain object
  -> persist through write repository
  -> return response
```

A query handler typically does this:

```text
call read repository
  -> throw NotFoundException if needed
  -> map SQL/read model to response DTO
  -> return response
```

Handlers are registered as **transient** services.

## 8. Domain Architecture

Domain objects own business rules, invariants, and state transitions.

Good domain responsibilities:

- create a valid entity;
- enforce state transitions;
- reject invalid business actions;
- expose safe behavior-oriented methods;
- throw stable application exceptions for business rule failures.

Example responsibilities for an accounting book:

```text
Create
Activate
Suspend
Archive
Prevent invalid status transitions
Prevent mutation after archive
```

The domain should not save itself. Persistence belongs to repositories.

The domain may depend on module-local SQL models when useful for rehydration, for example:

```csharp
AccountingBook.CreateFromSql(AccountingBookSqlModel model)
```

Keep that dependency narrow. The domain still must not know Dapper, SQL text, repositories, transactions, or HTTP.

## 9. SQL Models

SQL models are flat objects that represent database rows or read projections.

They are not domain objects.

They are useful because Dapper maps cleanly into simple POCOs, and query handlers can map them directly into response DTOs.

Rules:

- Read repositories return SQL/read models.
- SQL models may have simple mapping helpers.
- SQL models should not contain business behavior.
- Do not create generic marker abstractions like `ISqlModel`.

Example:

```text
AccountingBookSqlModel
  -> maps database row
  -> can map to domain or response
  -> does not enforce business transitions
```

## 10. Persistence Architecture

Apex uses:

- SQL Server;
- Dapper;
- DbUp migrations;
- application-generated TSID `BIGINT` IDs;
- UTC `DateTime` from `IClock.UtcNow`;
- explicit read/write separation;
- explicit transaction boundaries;
- optional sharding through `IShardResolver`.

The high-level flow is:

```text
Queries
  -> IReadDbConnectionFactory
  -> read database
  -> read repository
  -> SQL/read model

Commands
  -> IWriteTransactionRunner
  -> IWriteDbSession
  -> write repository
  -> write database
```

Read and write connection strings may point to the same physical database in local development and most business integration tests. They may point to different databases in production or read/write infrastructure tests.

Repositories resolve databases by module name. They must not hardcode connection string names.

## 11. Read Repositories

Read repositories are for queries.

They use `IReadDbConnectionFactory` and return SQL/read models.

Common methods:

```text
GetByIdAsync
ListAsync
SearchAsync
ExistsAsync
GetCurrentAsync
```

Read repositories should not:

- mutate data;
- use `IWriteDbSession`;
- start transactions;
- decide HTTP behavior.

A missing row usually returns `null`. The query handler decides whether that becomes a `NotFoundException`.

## 12. Write Repositories

Write repositories are for commands.

They use `IWriteDbSession`, which provides the active connection and transaction.

Common methods:

```text
InsertAsync
UpdateAsync
DeleteAsync
GetByIdForUpdateAsync
ExistsForUpdateAsync
ExistsOverlappingAsync
```

Write repositories must not open their own transaction. The command handler starts the transaction through `IWriteTransactionRunner`.

Race-sensitive checks and command-side consistency checks belong on the write side.

## 13. Database Conventions

Apex database conventions:

- Use Dapper only.
- Use explicit column lists; never `SELECT *`.
- Always parameterize SQL.
- Use `snake_case` table and column names.
- Use application-generated TSID `BIGINT` IDs.
- Do not use SQL identity columns for application entities.
- Use UTC `DateTime` from `IClock.UtcNow`.
- Avoid direct `DateTime.UtcNow` in application code.

Common column names:

```text
id
code
title
status
created_at
updated_at
owner_type
owner_id
```

Example table names:

```text
accounting_book
accounting_fiscal_year
accounting_journal_entry
```

## 14. Migrations

DbUp owns schema changes.

Production migrations live in the centralized migrator project:

```text
tools/Apex.DatabaseMigrator/Scripts/<ModuleName>/
```

Example:

```text
tools/Apex.DatabaseMigrator/Scripts/Accounting/
  000001_create_accounting_book.sql
  000002_create_fiscal_year.sql
```

Migration naming uses:

```text
000001_<description>.sql
```

Rules:

- Migrations are append-only once shared.
- Production migrations should not contain test data.
- Test-only migrations stay separate.
- Include explicit primary keys, foreign keys, unique constraints, indexes, and check constraints.
- Keep scripts deterministic and readable.

## 15. Sharding

Apex supports optional module-aware sharding.

When a table is sharded, repositories must resolve the physical table name through `IShardResolver`:

```text
IShardResolver.ResolveTableName(moduleName, logicalTableName, shardContext)
```

Rules:

- Do not hardcode shard-specific table names.
- Do not hardcode shard-specific connection strings.
- Sharding decisions belong in resolvers, not in handlers.
- Use cases may pass shard context, such as fiscal year, into repositories.
- Repositories use the shard resolver to choose the physical table.

## 16. Error Handling Architecture

Apex uses centralized exception handling middleware.

The flow is:

```text
Endpoint
  -> Handler
    -> Domain/Repository throws exception
      -> Global exception middleware
        -> logs error with traceId
        -> returns ProblemDetails
```

Expected application errors use Apex exception types:

```text
NotFoundException       -> 404
ConflictException       -> 409
BusinessRuleException   -> 422
ForbiddenException      -> 403
UnauthorizedAccessException -> 401
ValidationException     -> 400 or configured validation status
Unexpected exception    -> 500
```

Clients receive stable `snake_case` error codes.

Examples:

```text
accounting_book_not_found
accounting_book_code_already_exists
accounting_book_invalid_status_transition
fiscal_year_overlaps_existing_year
validation_failed
```

Application `ProblemDetails` responses follow this shape:

```json
{
  "type": "https://errors.apex.local/accounting_book_code_already_exists",
  "title": "Conflict",
  "status": 409,
  "detail": "Safe message.",
  "instance": "/api/v1/accounting/books",
  "traceId": "abc123xyz789",
  "errorCode": "accounting_book_code_already_exists"
}
```

Validation responses include an `errors` object:

```json
{
  "type": "https://errors.apex.local/validation_failed",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/v1/accounting/books",
  "traceId": "abc123xyz789",
  "errorCode": "validation_failed",
  "errors": {
    "Code": ["Code is required."],
    "Title": ["Title must not exceed 256 characters."]
  }
}
```

Do not expose stack traces, SQL text, connection strings, secrets, or unexpected internal exception messages to clients.

## 17. Dependency Injection

DI registration belongs inside the module.

Typical lifetimes:

```text
Repositories: scoped
Handlers:     transient
Validators:   transient
```

Module registration should hide internal wiring from the API startup project.

Mapster may remain referenced. Use it where it adds value. Manual mapping is still preferred for simple obvious mappings.

## 18. Testing Architecture

Apex uses layered testing. The goal is to test behavior at the cheapest useful level.

```text
HTTP smoke tests
  -> routing, binding, serialization, middleware, auth wiring

Handler integration tests
  -> real business behavior with real DI, DB, repos, transactions, migrations

Domain/unit tests
  -> pure rules, transitions, validators, value objects

Read/write infrastructure tests
  -> prove read/write separation when databases differ
```

### HTTP Smoke Tests

HTTP tests should stay thin.

They verify that the endpoint is wired correctly and that standard HTTP concerns work.

Usually test:

- one successful request;
- one representative error, such as validation or 404;
- ProblemDetails shape when relevant.

Do not put every lifecycle workflow or business rule in HTTP tests.

### Handler Integration Tests

Handler integration tests are the main business-behavior tests.

They use:

- real handlers;
- real DI;
- real SQL Server through Testcontainers;
- real DbUp migrations;
- real repositories;
- real transactions.

Use them for:

- lifecycle transitions;
- conflict rules;
- business rejections;
- multi-step workflows;
- transaction behavior;
- persistence assertions.

Arrange business data through real handlers where possible. Use direct SQL mainly for persisted-state assertions.

### Domain and Unit Tests

Use unit tests for pure code:

- domain transitions;
- invariants;
- value objects;
- validators;
- small mapping helpers.

They should not need SQL Server, HTTP, Testcontainers, or the full application host.

## 19. How to Add a New Capability

Recommended order:

```text
1. Add DbUp migration using 000001_... naming.
2. Add domain model, enum/value objects, and error constants.
3. Add SQL model.
4. Add read and write repositories.
5. Add use case requests, responses, validators, and handlers.
6. Add capability endpoint mapper.
7. Wire capability endpoint mapper into module endpoint mapper.
8. Register repositories, handlers, and validators in module DI.
9. Add HTTP smoke tests.
10. Add handler integration tests.
11. Add domain/unit tests.
```

This order keeps schema, domain behavior, persistence, API surface, and tests aligned.

## 20. What Belongs Where

| Concern | Belongs in | Does not belong in |
|---|---|---|
| HTTP routes and binding | Endpoint mapper | Handler/domain/repository |
| Business workflow | Handler | Endpoint/repository |
| State transitions | Domain | Endpoint/repository |
| SQL text | Repository | Endpoint/handler/domain |
| Transaction start | Handler via `IWriteTransactionRunner` | Repository/domain |
| DB row shape | SQL model | Domain entity only |
| Error-to-HTTP mapping | Middleware | Endpoint/handler |
| Schema changes | DbUp migration | Runtime code |
| Business behavior tests | Handler integration tests | Large HTTP test suites |

## 21. Common Anti-Patterns

Avoid these:

```text
SQL in handlers
Business rules in endpoints
try/catch in endpoints for normal business errors
Repositories starting their own transactions
Query handlers using write repositories
Command handlers using read repositories for consistency checks
SELECT *
SQL identity columns for application IDs
Direct DateTime.UtcNow
Hardcoded module-name strings
Generic ISqlModel marker interfaces
Broad persistence behavior on domain entities
Arranging business tests with raw SQL
Putting full lifecycle workflows in HTTP smoke tests
Hardcoding shard table names
```

## 22. Architecture Summary

Apex should feel simple to navigate:

```text
Find the module.
Find the capability.
Find the use case.
Read the handler.
Follow domain rules and repository calls from there.
```

The architecture is intentionally boring: modules define boundaries, capabilities keep related code together, handlers orchestrate behavior, domain objects protect business rules, repositories own SQL, migrations own schema, middleware owns error responses, and tests prove the system at the right level.
