# Apex Development Guide — Compact LLM Version

Purpose: build Apex modules consistently. Apex is a modular monolith: modules are business boundaries; capabilities are feature folders. Keep responsibilities split: endpoints=HTTP, handlers=use-case orchestration, domain=business rules, repositories=SQL, middleware=errors, migrations=schema, tests=behavior confidence.

## 1. Non-Negotiables

- Organize by business capability, not technical layer.
- Use Dapper only; no EF/ORM. SQL lives only in repositories.
- Separate read/write repositories. Queries use read repos. Commands use write repos inside `IWriteTransactionRunner`.
- Read repos return SQL/read models. Query handlers map SQL models to response DTOs.
- Write repos use `IWriteDbSession`; repositories never start transactions.
- DbUp owns schema in centralized migrator scripts; migration names use `000001_<description>.sql`.
- IDs are app-generated TSID `BIGINT`; no SQL identity columns.
- Time uses UTC `DateTime` from `IClock.UtcNow`; no direct `DateTime.UtcNow`.
- Errors use centralized exception middleware, RFC7807-style `ProblemDetails`, stable `snake_case` `errorCode`, public `traceId`.
- HTTP tests are smoke tests. Handler integration tests prove business behavior. Domain/unit tests prove pure logic.

## 2. Modules and Capabilities

Good module names: `Accounting`, `Assets`, `IdentityAccess`, `ReferenceData`, `Portfolio`, `Payments`. Bad: `Services`, `Repositories`, `Controllers`, `Infrastructure`, `Common`.

Module owns: domain concepts, capabilities, use cases, endpoints, repos, SQL models, DI, DB config/migration ownership, tests.

Default module shape:

```text
Apex.Modules.<ModuleName>/
  <ModuleName>Module.cs              // public const string Name = "<ModuleName>"
  DependencyInjection.cs             // Add<ModuleName>Module(...)
  Endpoints/<ModuleName>Endpoints.cs // top-level route group
  <CapabilityA>/
  <CapabilityB>/
```

Use the module constant everywhere module name is needed: read/write DB factories, transaction runner, shard resolver. Do not hardcode raw module-name strings.

API startup calls module registration and mapper only, e.g. `builder.Services.AddAccountingModule(...)`, `app.MapAccountingEndpoints()`. It must not manually register/map every internal service/capability.

Module endpoint mapper groups capability endpoints:

```text
/api/v1/accounting group -> MapAccountingBookEndpoints(), MapFiscalYearEndpoints(), ...
```

Organize module internals by capability:

```text
<CapabilityName>/
  <CapabilityName>Endpoints.cs
  Domain/<Entity>.cs, <Enum>.cs, <ValueObject>.cs, <CapabilityName>Errors.cs
  SqlModels/<Entity>SqlModel.cs
  Repositories/<CapabilityName>ReadRepository.cs, <CapabilityName>WriteRepository.cs
  UseCases/<UseCase>/Request.cs, Response.cs, Validator.cs, Handler.cs
```

Dependency direction: `Endpoints -> UseCases/Handlers -> Domain + Repositories -> SqlModels + Dapper`.

Rules: domain must not depend on ASP.NET Core, Dapper, repos, or infra; domain may narrowly depend on module-local SQL models for rehydration such as `CreateFromSql(...)`; endpoints contain no business rules; handlers contain no SQL; repositories decide no HTTP behavior; SQL models own no business behavior.

Cross-module: keep modules independent. Prefer small stable application contracts. Avoid depending on another module's internal domain model or tables. Add `Contracts/` only for public module contracts needed by other modules.

## 3. Persistence

Apex uses SQL Server, Dapper, DbUp, explicit read/write separation, explicit transaction boundaries, TSID `BIGINT` IDs, module-aware DB resolution, optional sharding, and Testcontainers.

Application abstractions: `IReadDbConnectionFactory`, `IWriteDbConnectionFactory`, `IWriteDbSession`, `IWriteTransactionRunner`, `IModuleDatabaseResolver`, `IShardResolver`, `ModuleDatabaseOptions`, `IIdGenerator`, `IClock`.

Read/write flow:

```text
Queries  -> IReadDbConnectionFactory -> read DB
Commands -> IWriteTransactionRunner + IWriteDbSession -> write DB
```

Read/write connection strings may point to the same DB for local/dev/business tests; infra tests and production can separate them. Repositories resolve DBs by module name, never hardcoded connection string names.

Module DB config supports read/write names and optional sharding:

```json
"Modules": { "Accounting": { "Database": {
  "ReadConnectionStringName": "AccountingReadDb",
  "WriteConnectionStringName": "AccountingWriteDb",
  "Sharding": { "Enabled": true, "Strategy": "FiscalYear", "DefaultShard": "Current" }
}}}
```

### Read Repositories

Use `IReadDbConnectionFactory`; used by query handlers; return SQL/read models; no mutation; no `IWriteDbSession`/`IWriteTransactionRunner`. Methods: `GetByIdAsync`, `ListAsync`, `SearchAsync`, `ExistsAsync`, `GetCurrentAsync`. Query handlers map models to responses and throw `NotFoundException` for missing resources that should become 404.

### Write Repositories

Use `IWriteDbSession`; used by command handlers; use `_session.Connection` and `_session.Transaction`; do not open connections or start transactions. Methods: `InsertAsync`, `UpdateAsync`, `DeleteAsync`, lifecycle commands, `GetByIdForUpdateAsync`, `ExistsOverlappingAsync`, `ExistsForUpdateAsync`. Transactionally consistent checks belong on write side.

### Handlers and Transactions

Command handler flow: validate request -> generate ID -> `now = IClock.UtcNow` -> `IWriteTransactionRunner.ExecuteAsync(moduleName, ...)` -> write-side consistency checks -> domain create/mutate -> write repo persist -> response. Commands throw app exceptions for expected failures.

Query handler flow: read repo -> if missing and resource expected, throw `NotFoundException` -> map SQL model to response. Queries start no transactions.

### SQL/DB Conventions

- Explicit column lists only; never `SELECT *`.
- Use `CommandDefinition` when cancellation tokens are needed.
- Always parameterize SQL; never concatenate user input.
- Names are `snake_case`.
- Tables include module/capability context: `accounting_book`, `accounting_fiscal_year`, `accounting_journal_entry`.
- Common columns: `id`, `code`, `title`, `status`, `created_at`, `updated_at`, `owner_type`, `owner_id`.
- IDs: app-generated TSID `BIGINT` via `IIdGenerator.NewId()`; no identity columns.
- Time: UTC `DateTime` via `IClock.UtcNow`; no direct system time.

### DbUp Migrations

Production migrations live under `tools/Apex.DatabaseMigrator/Scripts/<ModuleName>/`, e.g. `tools/Apex.DatabaseMigrator/Scripts/Accounting/`. Test-only migrations stay separate. Migrations are append-only after shared, deterministic, readable, and include explicit constraints/indexes.

Name scripts `000001_<description>.sql`, e.g. `000001_create_accounting_book.sql`. Typical content: tables, PKs, FKs, unique constraints, indexes, check constraints, audit columns.

### Locking and Sharding

Use write-side methods for race-sensitive checks; add SQL Server locking hints only when required and document them.

For sharded tables, repositories resolve physical table names through `IShardResolver.ResolveTableName(moduleName, logicalTableName, shardContext)`. Do not hardcode shard-specific connection strings/table names. Sharding decisions live in resolvers, not handlers; use cases pass required shard context such as fiscal year into repos.

## 4. Implementing a Capability

Checklist:

1. DbUp migration in centralized migrator using `000001_...`.
2. `Domain/`: model, state rules, enums/value objects, error constants.
3. `SqlModels/`: row/read model.
4. Read/write repositories.
5. Use cases: request, response, validator, handler.
6. Capability endpoint mapper.
7. Wire capability mapper into module endpoint mapper.
8. Register in module DI.
9. HTTP smoke tests.
10. Handler integration tests.
11. Domain/unit tests.

### Domain

Domain owns state, invariants, transitions, and business exceptions. It does not know ASP.NET Core, Dapper, repos, or infra. It may narrowly depend on module-local SQL models for rehydration. It should not save itself or expose broad persistence behavior. Invalid transitions throw `BusinessRuleException` with stable error codes. Use C# enums in code and strings in DB; mapping belongs in SQL model mapping or small helpers.

### Error Constants

Each capability centralizes lowercase `snake_case` errors, e.g. `accounting_book_not_found`, `accounting_book_code_already_exists`, `accounting_book_invalid_status_transition`.

### SQL Models

Flat POCOs matching DB rows. Read repos return them. They may have simple `MapToDomain()`. Domain may have `CreateFromSql(...)`. No generic `ISqlModel` marker interfaces.

### Handlers

One named handler per use case, one public `HandleAsync`. Examples: `CreateAccountingBookHandler`, `GetAccountingBookHandler`, `ActivateAccountingBookHandler`.

Command handlers generate IDs, use `IClock`, run transactions, check write-side consistency, call domain, persist, return response. Query handlers call read repos, handle missing data, map SQL model to response.

### Validators

Validate input shape/simple request rules only: required fields, length, format, ranges, allowed enum values, simple cross-field checks. Do not check DB uniqueness, state transitions, permissions, or transactional consistency.

### Endpoints

Endpoints define routes, bind requests, call handlers, return success results, attach metadata/tags/auth/OpenAPI. They do not contain business rules, SQL, normal business-error `try/catch`, manual normal error responses, or transactions. Capability endpoints map relative routes and are called from module group.

### DI and Mapping

Register inside module DI. Repo lifetime: scoped. Handler/validator lifetime: transient. Mapster may remain referenced and can be used where valuable; simple manual mapping is acceptable.

## 5. Error Handling

Centralized middleware converts exceptions to RFC7807-style `ProblemDetails`, logs with `traceId`, hides unexpected internals, and returns stable `errorCode` values.

Flow: `Endpoint -> Handler -> Domain/Repository throws -> GlobalExceptionHandlingMiddleware logs + returns ProblemDetails`.

No manual HTTP error responses for normal business failures. No endpoint `try/catch` for expected business errors.

Exception mapping:

```text
NotFoundException -> 404
ConflictException -> 409
BusinessRuleException -> 422
ForbiddenException -> 403
UnauthorizedAccessException -> 401
FluentValidation.ValidationException -> 400/configured validation status
Unexpected exception -> 500
```

Use few exception classes; use specific error codes for detail. Error codes are stable lowercase `snake_case`. Good: `fiscal_year_not_found`, `fiscal_year_overlaps_existing_year`, `account_code_already_exists`, `journal_entry_not_balanced`. Bad: `NotFound`, `FISCALYEARERROR`, `Error123`, `SomethingWentWrong`.

Application `ProblemDetails` shape:

```json
{
  "type": "https://errors.apex.local/<errorCode>",
  "title": "Conflict",
  "status": 409,
  "detail": "Safe message.",
  "instance": "/api/v1/accounting/fiscal-years",
  "traceId": "abc123xyz789",
  "errorCode": "fiscal_year_overlaps_existing_year"
}
```

Validation shape:

```json
{
  "type": "https://errors.apex.local/validation_failed",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/v1/accounting/books",
  "traceId": "abc123xyz789",
  "errorCode": "validation_failed",
  "errors": { "Code": ["Code is required."], "Title": ["Title must not exceed 256 characters."] }
}
```

Exception ownership: domain throws invariant/transition failures; handlers throw orchestration failures such as not found/conflict/permission/business-state; repositories usually let provider/database errors bubble unless specifically expected.

Security: clients may receive stable `errorCode`, safe message, public `traceId`. Never expose connection strings, SQL text, stack traces, unexpected internal messages, secrets, or config.

## 6. Testing

Layers: HTTP smoke, handler integration, domain/unit, separated read/write infra. Use cheapest test that proves behavior.

### HTTP Smoke Tests

Use `WebApplicationFactory`. Verify routing, binding, serialization, middleware, auth wiring, and `ProblemDetails` format. Per endpoint: one success + one representative error, usually 404 or validation. Avoid exhaustive business rules and lifecycle workflows through HTTP.

### Handler Integration Tests

Main business-behavior tests. Exercise real handlers, DI, DB, transactions, migrations, repositories. Use for lifecycle transitions, conflicts, business rejections, multi-step workflows, persistence verification, transaction behavior.

Arrange business data via real handlers, not raw SQL or HTTP. Raw SQL bypasses rules; HTTP adds transport overhead. Direct SQL is OK for persisted-state assertions. Assert typed exceptions and `ErrorCode` directly. Complex workflows belong here, e.g. `Create -> Activate -> Suspend -> Archive -> Verify`.

### Domain/Unit Tests

Use for pure logic: entity transitions, value objects, invariants, validators, small mapping helpers. No SQL Server, Testcontainers, ASP.NET host, HTTP client, or real DI unless needed.

### Read/Write Infrastructure Tests

Use only for read/write routing with separate DBs. Verify read factory uses read DB, write factory uses write DB, transaction runner writes only to write DB. Normal business integration tests may use one DB for both read/write strings.

### Test Data and Services

Production migrations define schema and contain no test data. Test-only migrations stay separate. Reset mutated DB state at each test start; keep tests deterministic; do not rely on order/shared mutable state; delete affected rows; preserve migration history. Resolve scoped services from a scope, never root provider.

## 7. Reference

### New Module Checklist

```text
[ ] Create Apex.Modules.<ModuleName>.
[ ] Add <ModuleName>Module.cs with Name constant.
[ ] Add DependencyInjection.cs.
[ ] Add Endpoints/<ModuleName>Endpoints.cs.
[ ] Add first capability folder.
[ ] Register module dependencies in API startup.
[ ] Map module endpoints in API startup.
[ ] Add module DB config.
[ ] Add DbUp scripts under tools/Apex.DatabaseMigrator/Scripts/<ModuleName>/ using 000001_... naming.
[ ] Add tests.
```

### New Capability Checklist

```text
[ ] Create <CapabilityName>/.
[ ] Add <CapabilityName>Endpoints.cs.
[ ] Add Domain/ model, enum/value objects, error codes.
[ ] Add SqlModels/ model; read repos return this model.
[ ] Add read/write repositories.
[ ] Add UseCases/<UseCase>/ request/response/validator/handler files.
[ ] Queries use read repo; commands use write repo + transaction runner.
[ ] Register repos scoped; handlers/validators transient.
[ ] Map capability endpoints from module mapper.
[ ] Add HTTP smoke, handler integration, and domain/unit tests.
```

### Naming

```text
Modules:       Apex.Modules.Accounting, Apex.Modules.Assets, Apex.Modules.IdentityAccess
Capabilities: FiscalYears, ChartOfAccounts, AccountingBooks, JournalEntries
Use cases:    CreateAccountingBook, GetAccountingBook, ActivateAccountingBook, ArchiveAccountingBook
Handlers:     CreateAccountingBookHandler, GetAccountingBookHandler
Repositories: AccountingBookReadRepository, AccountingBookWriteRepository
SQL models:   AccountingBookSqlModel
Error codes:  accounting_book_not_found, accounting_book_code_already_exists, accounting_book_invalid_status_transition
Tables:       accounting_book, accounting_fiscal_year, accounting_journal_entry
Columns:      created_at, updated_at, owner_type, owner_id
```

### Anti-Patterns

```text
SQL in handlers
Business-error try/catch in endpoints
Interface for every repository by default
Broad persistence behavior on domain entities
Generic ISqlModel marker interfaces
SELECT *
SQL identity columns for app IDs
Direct DateTime.UtcNow
Hardcoded module-name strings
Repositories starting transactions
Query handlers using write repos
Command handlers using read repos for consistency checks
Arranging handler tests with raw SQL
Putting lifecycle workflows in HTTP smoke tests
```

### Default Flow

```text
DbUp migration (000001_...)
 -> Domain model
 -> SQL model
 -> Read/write repositories
 -> Use case handlers
 -> Validators
 -> Endpoint mapper
 -> DI registration
 -> HTTP smoke tests
 -> Handler integration tests
 -> Domain/unit tests
```
