# Apex Architecture Guide for Coding Agents

## 1. Purpose and Authority

This document defines the target architecture of Apex. It is an execution contract for coding agents and LLMs that create, modify, review, or test Apex code.

Agents MUST read and follow this guide before changing the codebase. Existing code is not automatically authoritative: when existing code conflicts with this guide, the agent MUST preserve requested behavior while moving the touched area toward this target architecture. An agent MUST NOT perform an unrelated repository-wide refactor merely to enforce this guide.

Normative terms:

- **MUST** and **MUST NOT** are mandatory.
- **SHOULD** and **SHOULD NOT** apply unless a concrete technical reason is documented.
- **MAY** indicates an allowed option.

Task-specific user instructions override this guide. When an instruction requires an architectural exception, the agent MUST identify the exception explicitly in its handoff.

## 2. System Shape

Apex is a .NET 10 modular monolith organized around business modules, capabilities, and use cases.

The architecture has four primary project roles:

```text
src/
├── Apex.Api/                    # Composition root and HTTP host
├── Apex.Application/            # Shared application abstractions
├── Apex.Infrastructure/         # Shared technical implementations
└── Apex.Modules.<Module>/       # Independently owned business module
```

The following dependency direction is mandatory:

```text
Apex.Api
  ├── Apex.Infrastructure
  └── Apex.Modules.*
          └── Apex.Application

Apex.Infrastructure
  └── Apex.Application
```

Rules:

- `Apex.Api` MUST be the application composition root.
- `Apex.Api` MUST wire modules, infrastructure, authentication, middleware, logging, health checks, and endpoint mapping.
- `Apex.Api` MUST NOT contain business rules or persistence logic.
- `Apex.Application` MUST contain only genuinely shared abstractions and primitives.
- `Apex.Application` MUST NOT reference `Apex.Api`, `Apex.Infrastructure`, or any business module.
- `Apex.Infrastructure` MUST implement shared technical concerns and abstractions.
- `Apex.Infrastructure` MUST NOT contain module-specific business behavior, module repositories, or module SQL.
- Each module MUST own its domain behavior, use cases, repositories, endpoint composition, validation, and module registration.
- Modules MUST NOT depend on another module's internal implementation.
- New projects MUST NOT be introduced unless a project boundary provides real dependency or deployment value.

## 3. Module and Capability Boundaries

A module represents a major business boundary. A capability represents a cohesive area of behavior and data ownership inside a module.

```text
Apex.Modules.<Module>/
├── <Capability>/
│   ├── Domain/
│   ├── Repositories/
│   │   └── Rows/
│   ├── UseCases/
│   └── <Capability>Endpoints.cs
├── Contracts/                   # Public module contracts only when needed
├── IntegrationEvents/           # Public integration events only when needed
├── Endpoints/
│   └── <Module>Endpoints.cs
├── DependencyInjection.cs
└── <Module>Module.cs
```

Rules:

- Code MUST be grouped first by business capability, not by technical layer across the whole module.
- Domain code, repositories, rows, and use cases belonging to a capability MUST remain inside that capability.
- A capability MUST own the database access for the data it owns.
- Capability names MUST express business concepts, not implementation mechanisms.
- A capability MUST NOT become a miscellaneous shared folder.
- Shared code MUST be placed at the narrowest valid scope: use case, capability, module, then application-wide.
- Agents MUST NOT move code to a broader shared scope only to remove a small amount of duplication.
- Internal module types SHOULD be `internal` when external access is unnecessary.

## 4. Vertical-Slice Use Cases

Every externally triggered operation MUST be modeled as an explicit use case. Each use case owns its transport models, handler, endpoint, and validation relevant to that operation.

```text
<Capability>/UseCases/<Action><Subject>/
├── <Action><Subject>Endpoint.cs
├── <Action><Subject>Handler.cs
├── <Action><Subject>Request.cs       # When input exists
├── <Action><Subject>Response.cs      # When output exists
└── <Action><Subject>Validator.cs     # When structural validation exists
```

Rules:

- Each use case MUST have its own endpoint mapping file when exposed over HTTP.
- Each use case MUST have one primary handler.
- Handlers MUST expose an explicit asynchronous operation and accept a `CancellationToken`.
- Requests and responses MUST be use-case-specific contracts.
- Agents MUST NOT introduce generic command, query, request, response, or CRUD base classes.
- Agents MUST NOT add a mediator solely to dispatch use cases inside the monolith.
- A handler MUST orchestrate the use case, not implement infrastructure details.
- A handler MUST NOT contain raw SQL.
- A handler MUST NOT return domain entities or repository row types from an HTTP API.
- A handler MAY call multiple repositories owned by its module when the business operation requires it.
- A simple use case SHOULD remain simple; agents MUST NOT manufacture layers or interfaces without a concrete boundary or testing need.

## 5. Endpoint Architecture

Endpoint composition has three levels:

1. The use-case endpoint maps one operation.
2. The capability endpoint file creates the capability route group and maps its use cases.
3. The module endpoint file maps all capabilities and sits near the module root under `Endpoints/`.

Rules:

- Use-case endpoint files MUST contain HTTP concerns only: binding, route metadata, authorization metadata, handler invocation, and result conversion.
- Use-case endpoints MUST NOT contain business rules, database access, or transaction control.
- Capability endpoint files MUST compose endpoints and define shared route-group behavior.
- Module endpoint files MUST compose capability endpoints.
- `Program.cs` MUST call only module-level endpoint mappers, plus application-level system endpoints.
- Routes MUST be versioned and consistently grouped.
- Endpoints MUST use stable, unique names.
- Endpoints SHOULD declare useful OpenAPI metadata and documented response types.
- Cancellation tokens MUST flow from HTTP endpoints to handlers and repositories.

## 6. Authorization

Authorization MUST be enforced at both route-group and use-case levels.

Rules:

- Capability or module route groups MUST require authentication or broad module access by default.
- Individual use-case endpoints MUST declare operation-specific authorization policies where permissions differ.
- Anonymous access MUST be explicit through `AllowAnonymous()`.
- Business-data authorization MUST be verified inside the handler after loading the required business context.
- Route authorization MUST NOT be treated as sufficient when access depends on ownership, tenant, organization, book, fiscal year, or another data-derived condition.
- Handlers MUST use `ICurrentUser` or a dedicated authorization abstraction; they MUST NOT read `HttpContext` directly.
- Clients MUST NOT be trusted to provide authoritative ownership or tenant values without server-side verification.

## 7. Validation and Business Rules

Validation has two distinct levels.

### 7.1 Request validation

FluentValidation MUST be used for structural input rules such as required values, length, format, range, and pagination limits.

### 7.2 Domain validation

Domain entities and domain services MUST enforce business invariants and state transitions.

Rules:

- Request validators MUST NOT query the database.
- Database-dependent checks MUST occur in handlers through repositories.
- Business invariants MUST NOT exist only in validators or endpoints.
- Domain behavior MUST reject invalid state transitions even when the caller was already validated.
- Handlers MUST normalize input once at the boundary when normalization is part of the contract.
- Normalization rules MUST be deterministic and consistently applied before uniqueness checks and persistence.
- Database constraints MUST protect critical invariants such as uniqueness, required data, and allowed status values.
- Application checks improve error clarity; database constraints remain the final concurrency-safe protection.

## 8. Domain Model

The domain model protects write-side business behavior. It is not an ORM model and not an API contract.

Rules:

- Domain entities MUST expose behavior-oriented methods for meaningful state changes.
- Mutable domain properties SHOULD use private setters.
- Invalid domain state MUST be impossible to create through public APIs.
- Creation MUST go through a factory or constructor that enforces invariants.
- Rehydration from persistence MAY use a separate non-public factory.
- Rehydration MUST NOT rerun creation behavior or generate new business events.
- Handlers and repositories MUST NOT set protected state directly.
- Domain entities MUST NOT reference ASP.NET Core, Dapper, SQL connections, configuration, logging, or repository implementations.
- Domain entities MUST NOT be serialized directly as API responses.
- Navigation properties across capabilities or modules MUST NOT be introduced; store identifiers and resolve related data explicitly.
- Domain services SHOULD be introduced only when business behavior genuinely spans multiple entities and does not naturally belong to one entity.

## 9. Read and Write Model Separation

Apex uses lightweight CQRS: read and write paths are conceptually separated without requiring separate databases or messaging infrastructure.

### 9.1 Write paths

Write paths MUST use domain entities when behavior or invariants are involved.

```text
Request → Validator → Handler → Write Repository → Domain Entity
                           └── Transaction → Persisted State
```

### 9.2 Read paths

Read paths MUST return immutable row models or use-case-specific projections and MUST NOT reconstruct domain entities without a write-side reason.

```text
Request → Validator → Handler → Read Repository → Projection → Response
```

Rules:

- Read and write repositories MUST have separate interfaces.
- Read and write repositories MAY use the same physical database and connection infrastructure.
- Read separation does not imply read replicas, event sourcing, or separate data stores.
- Write repositories MUST load and persist domain entities.
- Read repositories MUST return capability-owned row types or projections.
- Repository row types MUST remain persistence-internal and MUST NOT become public API contracts.
- A reusable row type SHOULD represent a stable stored shape.
- A complex query SHOULD return a use-case-specific projection instead of continually expanding a shared row type.
- Queries SHOULD select only the columns they require.
- Read models MAY denormalize data within the owning module when performance or clarity requires it.

## 10. Repository Architecture

All application database access MUST go through repositories owned by the relevant capability.

Rules:

- Repositories MUST live inside their owning capability.
- Repository interfaces and implementations MAY remain together because the module owns both the abstraction and implementation.
- Repositories MUST be behavior- or query-oriented, not generic CRUD abstractions.
- Agents MUST NOT create `IRepository<T>`, a generic unit of work, or a generic Dapper base repository.
- A capability MUST normally have reusable read and write repositories, not one repository per use case.
- Repository methods MUST express caller intent clearly.
- Repositories MUST contain SQL, parameter binding, persistence mapping, and database-specific locking required by their contract.
- Repositories MUST NOT decide business workflows or perform authorization.
- Repositories MUST NOT commit, roll back, or independently own application transaction boundaries.
- Every command executed inside a transaction MUST receive the active transaction explicitly.
- SQL identifiers MUST be static. User input MUST be passed as parameters, never interpolated into SQL.
- Dynamic SQL fragments MUST be selected only from controlled application-defined alternatives.
- Repository methods MUST accept and pass a `CancellationToken`.
- Write operations SHOULD verify affected row counts when zero or multiple rows indicate concurrency or correctness failures.
- Database exceptions SHOULD be translated only when the application can provide a stable, meaningful error; otherwise they MUST propagate to global handling.

## 11. Dapper and SQL Rules

Dapper is the persistence mapper. SQL remains explicit and owned by the module.

Rules:

- Agents MUST NOT introduce Entity Framework Core or another ORM without an explicit architectural decision.
- SQL MUST list columns explicitly; `SELECT *` MUST NOT be used.
- SQL aliases MUST map deliberately to row properties.
- Queries MUST be parameterized.
- Date and time columns MUST use consistent UTC semantics.
- Monetary values MUST use an exact database type and MUST NOT use floating-point types.
- Status values stored as strings MUST be constrained in the database and mapped explicitly in code.
- Pagination MUST have deterministic ordering and a bounded page size.
- Search queries MUST account for appropriate indexes and wildcard behavior.
- Commands requiring pessimistic concurrency MUST use database-appropriate locks inside an explicit transaction.
- Locking hints MUST be used only with a documented concurrency purpose.
- Agents MUST review query plans or index requirements for new high-volume access patterns.

## 12. Transactions and Concurrency

Handlers or explicit module-local workflow orchestrators own transaction boundaries.

Rules:

- A write use case that must be atomic MUST use the appropriate transaction runner.
- Repositories MUST participate in the caller's transaction.
- Transactions MUST be as short as correctness permits.
- External network calls MUST NOT occur inside a database transaction unless the design explicitly requires and justifies it.
- A transaction MAY span multiple capabilities inside one module.
- A transaction MUST NOT span multiple modules.
- Exactly one module MUST own a transaction.
- Cross-capability transactions MUST be explicit and driven by a business workflow.
- Cross-capability orchestration SHOULD live in the capability that owns the operation.
- If no existing capability owns the workflow, a dedicated workflow capability SHOULD be created.
- Ordinary handlers SHOULD NOT depend casually on concrete repositories from unrelated capabilities.
- Concurrency correctness MUST rely on database constraints, appropriate isolation, locks, row versions, idempotency, or an explicit combination of them.
- A pre-insert existence query alone MUST NOT be considered a concurrency-safe uniqueness guarantee.
- Expected concurrency conflicts MUST be converted to stable conflict errors when practical.

## 13. Module Communication

Modules are autonomous business boundaries inside one deployable application. They communicate only through explicit contracts or integration events.

### 13.1 Synchronous module contracts

Use a synchronous contract when the caller requires an immediate result to complete its operation.

Rules:

- Public module contracts MUST be narrow, stable, and business-oriented.
- Contracts MUST use contract-specific request and result types.
- Contracts MUST NOT expose domain entities, repository rows, repositories, internal handlers, or database connections.
- A consuming module MUST depend only on the providing module's public contract surface.
- The providing module MUST retain control of its own transaction and invariants.
- A synchronous cross-module call MUST NOT enlist both modules in one database transaction.

### 13.2 Integration events

Use an integration event when consumers may react independently and the publisher does not require their immediate result.

Rules:

- Integration events MUST describe completed business facts in past tense.
- Event contracts MUST contain stable identifiers and necessary facts, not internal entities.
- Publishers MUST NOT assume which modules consume an event.
- Consumers MUST be idempotent when delivery may be repeated.
- Reliable event delivery MUST use an outbox or an equally explicit durability mechanism when loss is unacceptable.
- Integration events imply eventual consistency; agents MUST make that consistency model visible in implementation and tests.

### 13.3 Prohibited cross-module access

A module MUST NOT:

- access another module's tables;
- use another module's repository;
- call another module's internal handler;
- reference another module's domain entity;
- update another module's data inside its transaction;
- depend on another module's internal namespace;
- share mutable domain state with another module.

When choosing a communication mechanism:

1. If an immediate result is required, use a synchronous module contract.
2. If the action is an independent reaction to a completed fact, use an integration event.
3. If atomicity across modules appears necessary, redesign ownership or the workflow; do not create a distributed transaction.

## 14. General Database and Shards

Apex supports a General Database and explicitly routed shard databases.

Rules:

- The General Database MUST hold global data and the shard directory.
- A module MAY use only the General Database or both the General Database and shards.
- Data placement MUST be decided by business ownership and access patterns, not convenience.
- Shard selection MUST start from an explicit business partition value.
- Every shard operation MUST construct an explicit `ShardKey` containing an entity type and discriminator.
- A shard key MUST NOT be inferred from a TSID, primary key, connection string, ambient context, or arbitrary entity lookup.
- The same business partition MUST resolve consistently unless an explicit reassignment process changes it.
- Connection names returned by the shard directory MUST be resolved through configuration allow-listing.
- Business modules MUST depend on shard abstractions, not infrastructure implementations.
- A repository MUST know whether its data belongs to the General Database or a shard.
- Agents MUST NOT create joins, foreign keys, or transactions across databases or shards.
- Cross-shard workflows MUST use explicit coordination, idempotency, and eventual consistency.
- Shard-routing failures MUST fail closed and MUST NOT silently fall back to the General Database or a default shard.
- Routing caches MUST be bounded, expiring, and invalidatable.
- Schema compatibility MUST be checked before routing work to a shard.

## 15. Connections

Rules:

- General Database access MUST use `IGeneralConnectionFactory` or its approved successor.
- Shard access MUST use `IShardConnectionFactory` with an explicitly resolved `ShardKey`.
- Connection strings MUST come from configuration and MUST NOT be embedded in module code.
- Connection factories MUST own connection lifecycle mechanics, not business transactions.
- Scoped connection factories MAY reuse a connection within a request or transaction.
- Connections and transactions MUST be disposed deterministically.
- Read repositories MUST not accidentally execute outside an active transaction when the use case requires a consistent transactional read.

## 16. Identifiers and Time

Rules:

- Persistent entity identifiers MUST use `long` values generated from TSID unless a domain-specific exception is explicitly approved.
- New identifiers MUST be generated through `IIdGenerator`.
- Code MUST NOT depend on the internal bit layout of a TSID for sharding or business behavior.
- Identifiers MUST be treated as opaque values outside infrastructure-level generation and conversion.
- Application time MUST come through `IClock` when it affects business behavior, persistence, or tests.
- Business code MUST NOT call `DateTime.Now` or `DateTime.UtcNow` directly.
- Stored timestamps MUST represent UTC unless a field explicitly models a local business date or local time.

## 17. Errors and HTTP Problem Details

Expected failures MUST use stable application exceptions and error codes. The API converts them centrally to `ProblemDetails`.

Rules:

- Agents MUST reuse the established exception taxonomy.
- Error codes MUST be stable, machine-readable, lowercase identifiers.
- Error messages MUST be safe for clients and MUST NOT expose SQL, connection strings, secrets, stack traces, or internal topology.
- Validation errors MUST return structured field errors.
- Unexpected exceptions MUST be logged and returned as a generic server error.
- Exception-to-HTTP mapping MUST remain centralized in global exception handling.
- Endpoints MUST NOT duplicate global exception mapping with broad `try/catch` blocks.
- A short Nanoid trace ID MUST be attached to error responses and logs.
- Trace IDs MUST support correlation but MUST NOT contain business or personal data.
- Domain-specific error-code constants SHOULD live with the owning capability.

## 18. Dependency Injection

Rules:

- `Apex.Api` MUST register infrastructure and modules through explicit extension methods.
- Each module MUST own its registrations in `DependencyInjection.cs`.
- Repositories and transaction-scoped services MUST use compatible scoped lifetimes.
- Stateless handlers and validators MAY be transient.
- Shared immutable services MAY be singleton only when their dependencies and state are singleton-safe.
- Service locator usage MUST NOT be introduced.
- Handlers SHOULD use constructor injection.
- Configuration required by infrastructure MUST be validated at startup when practical.
- Agents MUST remove unused configuration parameters and registrations from touched code.

## 19. Migrations and Schema Ownership

DbUp SQL scripts define database evolution.

Rules:

- Every schema change MUST be delivered through a new forward-only migration.
- An already-applied migration MUST NOT be edited.
- Migration filenames MUST use the repository's ordered numbering convention.
- General Database and shard migrations MUST remain distinguishable.
- Every table and migration MUST have one owning module or infrastructure concern.
- A module MUST NOT modify another module's tables directly.
- Tables MUST define primary keys, required constraints, and business-critical uniqueness constraints.
- Status columns SHOULD have check constraints when the allowed set is controlled.
- Foreign keys MAY be used only inside the same database and compatible ownership boundary.
- Cross-module and cross-shard relationships MUST be represented by identifiers without database foreign keys.
- Indexes MUST be based on actual access paths, uniqueness, ordering, and locking needs.
- Destructive or irreversible migrations MUST include an explicit rollout and data-preservation plan.
- Application and migration compatibility MUST support the intended deployment sequence.

## 20. Testing Architecture

Agents MUST test at the lowest layer that proves the required behavior and MUST add higher-level tests where integration boundaries matter.

### 20.1 Domain tests

Domain tests MUST cover:

- valid creation;
- normalization owned by the domain;
- allowed state transitions;
- rejected state transitions;
- invariant enforcement;
- timestamps or state changes resulting from behavior.

Domain tests MUST NOT require a database or web host.

### 20.2 Repository contract tests

Repository contract tests MUST use the real supported database engine and verify:

- SQL syntax and mapping;
- inserts, updates, and query projections;
- constraints and duplicate behavior;
- transaction participation and rollback;
- locking or concurrency behavior when relevant;
- cancellation where practical.

Mocking Dapper or SQL strings is not a substitute for repository contract tests.

### 20.3 Handler integration tests

Handler tests SHOULD use real repositories and a real database when database behavior is part of the use case. They MUST cover:

- successful orchestration;
- validation failure;
- not-found and conflict paths;
- business-rule failures;
- transaction commit and rollback;
- authorization based on business data;
- idempotency or concurrency when relevant.

### 20.4 HTTP integration tests

HTTP tests MUST verify the public contract:

- route and method;
- authentication and authorization;
- request binding and validation shape;
- status code and response body;
- `ProblemDetails` structure and stable error code;
- important persistence effects.

### 20.5 Sharding tests

Shard-related changes MUST verify:

- explicit key construction;
- correct directory resolution;
- cache expiry and invalidation when affected;
- unavailable assignment and schema mismatch failures;
- absence of silent fallback;
- correct physical database selection.

### 20.6 Test rules

- Tests MUST be deterministic and isolated.
- Business time MUST be controllable through `IClock`.
- Test IDs SHOULD be deterministic when exact values matter.
- Integration tests MUST own or isolate their test data.
- Tests MUST assert behavior, not implementation trivia.
- Agents MUST run the relevant test projects after a change.
- If tests cannot be run, the agent MUST state exactly what was not verified and why.

## 21. Observability, Security, and Operational Rules

Rules:

- Structured logging MUST be used.
- Logs MUST include useful identifiers and the trace ID where relevant.
- Secrets, credentials, tokens, full connection strings, and sensitive payloads MUST NOT be logged.
- Authentication configuration MUST fail safely.
- Production HTTPS requirements MUST NOT be weakened for local convenience.
- Health endpoints SHOULD distinguish liveness from dependency readiness when operational needs require it.
- Retry policies MUST be limited to transient failures.
- A database command MUST NOT be retried blindly when doing so can duplicate a non-idempotent write.
- External side effects MUST use idempotency where retries or repeated delivery are possible.
- Agents MUST consider query volume, pagination, indexes, connection use, and transaction duration for every new data path.

## 22. Naming Rules

Use names that reveal business intent.

```text
Module project:             Apex.Modules.<Module>
Capability:                 <PluralBusinessConcept>
Use case:                   <Verb><Subject>
Handler:                    <Verb><Subject>Handler
Endpoint:                   <Verb><Subject>Endpoint
Request:                    <Verb><Subject>Request
Response:                   <Verb><Subject>Response
Validator:                  <Verb><Subject>Validator
Read repository interface: I<Subject>ReadRepository
Write repository interface:I<Subject>WriteRepository
Repository row:             <Subject>Row
Module contract:            I<Module>Module or a narrow business contract
Integration event:          <CompletedBusinessFact>
```

Rules:

- Avoid ambiguous names such as `Manager`, `Helper`, `Processor`, `Common`, or `Utils` unless the responsibility is genuinely precise.
- Avoid suffixing domain types with `Entity` merely because they are persisted.
- Do not use `SqlModel` as a universal term. Use `Row` for persistence mapping and `Response` or a named projection for use-case output.
- Public names MUST remain stable when they form API or module contracts.

## 23. Agent Implementation Procedure

For every coding task, the agent MUST follow this sequence.

### Step 1: Establish scope

- Read the request and identify the owning module, capability, and use case.
- Inspect the nearby implementation, migrations, registrations, and tests.
- Identify whether data belongs in the General Database or a shard.
- Identify authorization, transaction, concurrency, and compatibility requirements.
- Do not modify unrelated areas.

### Step 2: State the design internally before editing

Determine:

- the business invariant;
- the use-case request and response;
- the domain behavior required;
- read and write repository operations;
- transaction owner and boundary;
- required locks, constraints, or idempotency;
- module communication mechanism, if any;
- tests that prove correctness.

If these cannot be determined without a business decision, the agent MUST ask for clarification rather than inventing behavior.

### Step 3: Implement from the center outward

Preferred order:

1. migration and constraints, when required;
2. domain behavior and errors;
3. repository contracts and rows/projections;
4. repository SQL implementation;
5. request, response, validator, and handler;
6. use-case endpoint;
7. capability and module endpoint composition;
8. dependency injection;
9. tests and documentation.

The agent MAY change this order when safe migration sequencing or test-driven work requires it.

### Step 4: Verify

- Build the affected projects.
- Run focused tests, then the broader relevant test suite.
- Verify migrations against the real supported database engine.
- Review the final diff for accidental changes, dependency violations, missing cancellation, unsafe SQL, missing authorization, and transaction mistakes.
- Confirm that no secrets or generated build artifacts were added.

### Step 5: Handoff

The final report MUST state:

- what behavior changed;
- important architectural decisions;
- migrations or operational considerations;
- tests executed and their results;
- unresolved risks or unverified items.

The report SHOULD be concise and MUST NOT claim verification that was not performed.

## 24. Decision Procedures

### 24.1 Where should code live?

1. Used by one use case only: keep it in the use-case folder.
2. Used by several use cases in one capability: keep it in that capability.
3. Used by several capabilities in one module: place it at module scope only if it represents a stable module concept.
4. Used across modules: expose a narrow contract or integration event.
5. Technically shared across the application: place an abstraction in `Apex.Application` and an implementation in `Apex.Infrastructure` only when the concern is genuinely application-wide.

### 24.2 Should this be a domain entity or read model?

1. Must protect behavior or invariants: use a domain entity on the write path.
2. Only transports query results: use a row or projection.
3. Public HTTP output: use a use-case response.
4. Cross-module communication: use a public contract type.

### 24.3 Which repository should be used?

1. Query without domain behavior: read repository.
2. Load or persist an aggregate for behavior: write repository.
3. Data belongs to another capability in the same module: use explicit module-local orchestration.
4. Data belongs to another module: use its public contract or an integration event; never its repository.

### 24.4 Should the workflow be transactional?

1. Multiple changes must succeed or fail together inside one module: one module-owned transaction.
2. Operation spans modules: no shared transaction; use contracts/events and explicit consistency handling.
3. Operation spans shards or databases: no shared transaction; use coordination, idempotency, and eventual consistency.

### 24.5 Contract or integration event?

1. Caller needs an immediate answer to proceed: synchronous module contract.
2. Publisher announces a completed fact and does not need consumer results: integration event.
3. Caller appears to require cross-module atomicity: reconsider ownership; do not introduce a distributed transaction.

## 25. Prohibited Patterns

Agents MUST NOT introduce:

- business logic in endpoints, middleware, repositories, or `Program.cs`;
- raw SQL outside capability repositories or migration scripts;
- generic repositories or generic units of work;
- one repository per use case without a concrete reason;
- direct cross-module database, repository, handler, or domain access;
- cross-module or cross-shard transactions;
- implicit shard selection;
- shard inference from primary keys;
- silent shard fallback;
- domain entities as API response models;
- database queries inside FluentValidation validators;
- `HttpContext` access inside domain or application handlers;
- direct business-time calls instead of `IClock`;
- unparameterized SQL or user-controlled SQL fragments;
- `SELECT *`;
- remote calls inside database transactions without explicit justification;
- broad catch-and-ignore behavior;
- leaking internal exception details to clients;
- mutable global state;
- service locator patterns;
- abstractions created only for speculative future use;
- repository-wide refactors unrelated to the requested task.

## 26. Completion Checklist

Before declaring a task complete, the agent MUST confirm all applicable items.

### Architecture

- [ ] The owning module and capability are clear.
- [ ] The change follows dependency direction.
- [ ] No module internals are accessed across module boundaries.
- [ ] Shared code is placed at the narrowest valid scope.

### Use case and API

- [ ] The use case has its own handler and endpoint file.
- [ ] Request, response, and validation are operation-specific.
- [ ] Endpoint code contains no business or persistence logic.
- [ ] Group-level and operation-level authorization are correct.
- [ ] Business-data authorization is enforced in the handler where required.
- [ ] Cancellation flows through every asynchronous layer.

### Domain and persistence

- [ ] Domain invariants and transitions are protected.
- [ ] Reads use rows/projections; writes use domain entities where behavior exists.
- [ ] Read and write repository interfaces remain separated.
- [ ] All SQL is parameterized and owned by the capability.
- [ ] Constraints and indexes support correctness and access patterns.
- [ ] Transaction ownership and locking are explicit.
- [ ] Affected-row and concurrency behavior are handled.

### Sharding and modules

- [ ] Data placement is explicit.
- [ ] Shard access uses an explicit business-derived `ShardKey`.
- [ ] No cross-database join or transaction was introduced.
- [ ] Cross-module communication uses a public contract or integration event.
- [ ] Event consumers are idempotent when required.

### Quality

- [ ] Stable error codes and safe `ProblemDetails` behavior are present.
- [ ] Migrations are forward-only and correctly scoped.
- [ ] Domain, repository, handler, HTTP, and routing tests exist as applicable.
- [ ] Relevant builds and tests pass.
- [ ] Logs contain no secrets or sensitive payloads.
- [ ] The final diff contains no unrelated edits or generated artifacts.

## 27. Final Architectural Summary

Apex is a modular monolith with strict business ownership. Modules contain capabilities; capabilities contain vertical-slice use cases. Each HTTP use case owns its endpoint and handler. Capability and module endpoint files only compose routes.

All database access goes through capability-owned, Dapper-based repositories. Read and write interfaces are separated: write paths use domain entities to enforce behavior, while read paths return rows or projections. Handlers own transactions, and a transaction may cross capabilities only inside one owning module. Transactions never cross modules, databases, or shards.

Modules communicate through narrow synchronous contracts when an immediate result is required and through integration events for independent reactions and eventual consistency. They never access each other's tables, repositories, handlers, or domain entities.

General and sharded data placement is explicit. Shard routing always begins with a business-derived `ShardKey`; it is never inferred from an identifier or ambient state. The database protects critical invariants through constraints, while the domain provides meaningful behavior and errors.

Coding agents MUST favor clear ownership, explicit behavior, minimal abstractions, safe persistence, deterministic tests, and narrowly scoped changes.
