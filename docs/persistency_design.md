# Persistence Design

## How to use this guide

This document is the implementation contract for persistence work. The words
**must**, **must not**, **required**, and **forbidden** are normative. Examples
illustrate the rules but do not override them. When an example conflicts with a
normative rule, follow the rule.

Before writing code, classify the task with the decision procedure below. Do
not invent a new persistence pattern when one of the defined paths applies. If
the requested behavior requires an undefined cross-database workflow,
transaction boundary, shard discriminator, or error policy, stop and request an
architecture decision.

### Implementation decision procedure

1. Identify the persisted entity and decide whether it belongs in the General
   Database or one shard database. An entity cannot use both.
2. For a sharded entity, identify its stable business discriminator before
   creating repository code. Do not use an editable value.
3. Decide whether the use case is a query or command.
4. Use the entity's read repository for queries and write repository for
   commands, including command-side reads and consistency checks.
5. Choose the normal row entity unless the query meets one of the explicit
   projection-row criteria in this document.
6. Keep the transaction within the selected physical database.
7. Add or update the migration for that database role.
8. Add migration-backed repository contract tests against SQL Server.
9. Run the completion checklist at the end of this document.

## Scope

The system uses one General Database and multiple shard databases. A module may
use only the General Database or both the General Database and shard databases.
`AccountingBook` and `FiscalYear` are examples only; routing infrastructure is
not entity-specific.

All database access performed by modules goes through repositories. Module
handlers, domain entities, and business services never use Dapper, SQL,
`DbConnection`, connection strings, logical shard IDs, or shard-directory
tables. Persistence infrastructure such as the shard directory and database
migrator may access databases directly because they implement routing and
migration concerns rather than module business behavior.

## Identifiers

Every business/domain entity primary key is a `long`, stored as SQL Server
`bigint` and generated through the configured TSID library. TSID is only an
implementation of `IIdGenerator`; domain models and repository contracts
contain only `long` IDs. Persistence-infrastructure records, including shard
directory rows, may use infrastructure-specific identifiers.

## Data placement

General entities live in the General Database. Sharded entities live in exactly
one shard database. Entities contain scalar values, value objects, and related
entity IDs; navigation objects are forbidden.

Database foreign keys are valid only when both rows always live in the same
physical database. Cross-database references are logical IDs.

Use this placement table:

| Question | Placement |
| --- | --- |
| Is the entity global, configuration-like, directory data, or required before shard routing? | General Database |
| Does one stable business identity own the entity and all transactional access stays within that identity? | One shard database |
| Would one operation need to atomically update the General Database and a shard, or two shards? | The design is incomplete; request a workflow decision |

Once selected, placement is part of the entity's persistence contract. Do not
route some rows of a General entity to shards or duplicate a sharded entity in
the General Database as an informal cache.

## Shard keys

`ShardKey` is a value type containing `EntityType` and `Discriminator`. Its
constructor rejects blank values and trims surrounding whitespace. Trimming is
not business normalization.

Example:

```csharp
new ShardKey("FiscalYear", "HAMI-1-2025");
```

`EntityType` identifies the sharded entity category. `Discriminator` identifies
its business routing identity. The discriminator must be stable, immutable,
deterministic, culture-invariant, and based on business identity. Editable
titles and descriptions must never be used. Business-specific normalization is
not part of the current design; callers must consistently produce the same
discriminator for the same business identity.

Repositories construct shard keys explicitly, either directly or through a
typed `IShardKeyFactory<TPartition>`. Attributes and reflection do not discover
discriminators. For a given entity type, use one construction path consistently;
do not duplicate discriminator-building logic across repositories.

## General Database directory

The General Database owns `Shards` and `ShardAssignments`:

```text
Shards
  id, connection_name, status, schema_version, timestamps, rowversion

ShardAssignments
  entity_type, discriminator, shard_id, status, timestamps, rowversion
```

`ShardAssignments` has a primary key on `(entity_type, discriminator)` and maps
each shard key to one logical shard. Only active assignments on routable,
schema-ready shards serve traffic.

Connection credentials never appear in the directory. `connection_name` is an
allow-listed key resolved through configuration or a secret store.

## Sample configuration

The General Database and every shard database have a logical connection name.
`Shards.connection_name` must reference one of the configured shard names.

```json
{
  "Sharding": {
    "GeneralConnectionStringName": "GeneralDb",
    "RequiredSchemaVersion": "1",
    "RoutingCacheTtlSeconds": 30,
    "RoutingCacheMaxEntries": 10000
  },
  "ConnectionStrings": {
    "GeneralDb": "Server=sql-general;Database=ApexGeneral;Encrypt=True;",
    "AccountingShard01": "Server=sql-shard-01;Database=ApexAccounting01;Encrypt=True;",
    "AccountingShard02": "Server=sql-shard-02;Database=ApexAccounting02;Encrypt=True;"
  }
}
```

Example General Database directory rows:

```text
Shards
  shard-01 -> AccountingShard01 -> ACTIVE -> schema version 1
  shard-02 -> AccountingShard02 -> ACTIVE -> schema version 1

ShardAssignments
  FiscalYear + HAMI-1-2025 -> shard-01 -> ACTIVE
  FiscalYear + HAMI-2-2025 -> shard-02 -> ACTIVE
```

Production connection-string values should come from environment variables or a
secret store. Only their logical names belong in `Shards`.

## Connection factories

General repositories use:

```csharp
public interface IGeneralConnectionFactory : IAsyncDisposable
{
    DbTransaction? Transaction { get; }
    bool HasActiveTransaction { get; }

    Task<DbConnection> OpenAsync(
        CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(
        CancellationToken cancellationToken = default);
    Task CommitAsync(
        CancellationToken cancellationToken = default);
    Task RollbackAsync(
        CancellationToken cancellationToken = default);
}
```

`IGeneralConnectionFactory` is scoped. Within that scope, `OpenAsync` reuses
its open connection. When a transaction is active, every command must use both
the connection returned by `OpenAsync` and the factory's `Transaction`.
Repositories do not dispose the factory-owned connection or transaction.
Starting a second general transaction is forbidden. Commit without an active
transaction is an error; rollback without an active transaction is a no-op.
Prefer `IGeneralTransactionRunner` at the command-workflow boundary instead of
manually coordinating begin, commit, and rollback in handlers.

Sharded repositories use:

```csharp
public interface IShardConnectionFactory
{
    Task<IShardConnection> OpenAsync(
        ShardKey shardKey,
        bool beginTransaction = false,
        CancellationToken cancellationToken = default);
}
```

The returned connection owns the provider connection and optional transaction:

```csharp
public interface IShardConnection : IAsyncDisposable
{
    ShardLocation Location { get; }
    DbConnection Connection { get; }
    DbTransaction? Transaction { get; }
}
```

Sharded repositories dispose `IShardConnection` with `await using`. Pass
`beginTransaction: true` only for a command workflow that requires a local
transaction, then execute all commands through that returned connection and
transaction. Do not resolve a shard separately and then open an unrelated
provider connection.

`IShardConnectionFactory` resolves `ShardAssignments`, verifies assignment and
shard status and schema version, resolves the allow-listed connection name, and
opens the provider connection. Repositories never select a physical shard.

### Routing failures

Do not replace routing failures with empty results or generic not-found results:

| Condition | Required exception |
| --- | --- |
| No active assignment exists | `ShardAssignmentNotFoundException` |
| The shard or configured connection is unavailable | `ShardUnavailableException` |
| The shard schema version is behind the required version | `ShardSchemaMismatchException` |

Application-level mapping of these exceptions is a separate concern. Repository
code must preserve the specific failure.

## Repositories and Dapper

Every module repository has an interface in its business module. This interface
requirement applies to repositories owned by business modules; infrastructure
components such as the shard directory and migration runner do not need
repository-style interfaces. Handlers and business services depend on repository
interfaces, which keeps tests independent from SQL and Dapper implementations.
Repository implementations contain SQL and Dapper. General repository
implementations depend on
`IGeneralConnectionFactory`; sharded repository implementations depend on
`IShardConnectionFactory` and accept the discriminator or a complete `ShardKey`
required for routing.

### Read and write repository separation

Each persisted entity has separate read and write repositories:

```text
IAccountingBookReadRepository -> AccountingBookReadRepository
  -> query operations
  -> returns AccountingBookRow or purpose-specific projection rows
  -> does not mutate data
  -> does not acquire update locks
  -> does not start transactions

IAccountingBookWriteRepository -> AccountingBookWriteRepository
  -> command operations
  -> inserts, updates, and deletes
  -> performs command-side existence checks and locked reads
  -> returns domain entities when rehydration is required
  -> participates in the transaction started by the command workflow
```

Query handlers depend only on read repositories. Command handlers depend only on
write repositories. Command-side consistency checks must not call a read
repository because those checks must use the same physical connection,
transaction, and locking semantics as the mutation.

Use the naming convention `I<Entity>ReadRepository` and
`I<Entity>WriteRepository` for contracts, with `<Entity>ReadRepository` and
`<Entity>WriteRepository` as their implementations. Both repositories may reuse
the same normal row entity, but they have different behavioral responsibilities.
This separation is about query versus command semantics; it does not imply
separate read and write databases or connection strings.

Use this module layout unless the module already has a more specific established
layout:

```text
<Module>/<Entities>/
  Domain/
    <Entity>.cs
  Repositories/
    I<Entity>ReadRepository.cs
    I<Entity>WriteRepository.cs
    <Entity>ReadRepository.cs
    <Entity>WriteRepository.cs
    Rows/
      <Entity>Row.cs
  UseCases/
    <UseCase>/
      <UseCase>Handler.cs
      <UseCase>Request.cs       # only when input is required
      <UseCase>Response.cs      # only when output is required
      <UseCase>Validator.cs     # only when request validation is required
      <UseCase>Endpoint.cs      # only when exposed over HTTP
```

Register repository interfaces, implementations, handlers, and validators in
the module's dependency-injection entry point. Business code must not inject a
repository implementation by its concrete type.

Dapper query results always map to typed row entities. `dynamic` results are
forbidden. Anonymous objects remain valid for Dapper parameters.

Use the smallest useful representation set for a normal entity:

```text
Domain behavior       -> AccountingBook
Database row          -> AccountingBookRow
HTTP contract         -> request/response type
Dapper parameters     -> anonymous object allowed
```

The normal row entity belongs under `Repositories/Rows/` and represents the full
database shape of that entity. Query repositories may return it. Query handlers
map it to endpoint responses. Command repositories use the same row entity and
map it to the domain entity before returning.

Do not create a read DTO that merely duplicates the row entity. Do not create
separate detail and summary rows only because two queries select different
column subsets. Use the complete normal row by default.

Add another row type only when at least one of these conditions is true:

- a join across multiple entities;
- an aggregate or report;
- a denormalized projection;
- calculated columns that do not belong to the normal entity row;
- the query deliberately omits a large payload column, and a test, execution
  plan, or measurement demonstrates that selecting the full row is harmful.

Name such types after their purpose, for example `AccountingBookUsageReportRow`,
not as another generic copy of the entity. Row entities contain no business
behavior. Domain entities never depend on row entities, and row entities are
never returned directly as HTTP responses.

If none of the listed conditions applies, use `<Entity>Row`. Do not create a
new projection type based only on preference or anticipated future use.

### Repository implementation procedure

For a query:

1. Add the method to `I<Entity>ReadRepository`.
2. Implement SQL and Dapper mapping in `<Entity>ReadRepository`.
3. Return a typed row or a justified purpose-specific projection row.
4. Map the row to the use-case response in the query handler.
5. Add a repository contract test for the SQL and mapping.

For a command:

1. Add the method to `I<Entity>WriteRepository`.
2. Keep existence checks, locked reads, and mutations in the write repository.
3. Use the same connection and transaction for all command-side checks and
   mutations.
4. Rehydrate and return a domain entity only when subsequent domain behavior
   requires it.
5. Add contract tests for persistence and row-to-domain rehydration.

## Transactions and workflows

A local ACID transaction targets exactly one physical database: either the
General Database or one shard database. Normal transactions must never span the
General Database and a shard or two different shards.

Cross-database workflows, when introduced, must be persisted, idempotent,
retryable, and state-driven. The current implementation does not provide a
generic provisioning or reconciliation workflow. Shard assignments are managed
outside module business workflows and routing consumes only active assignments.

## Migrations

DbUp migrations are separated by database role:

```text
Scripts/General/
Scripts/Shard/
```

General migrations run once. Shard migrations run independently against every
registered shard. The shard migration set may be empty until the first sharded
entity is introduced. All routable shards must have the application-required
schema version. A behind or failed shard cannot serve normal traffic.

For every schema change:

1. Put the script in `Scripts/General/` for a General entity or
   `Scripts/Shard/` for a sharded entity. Never place the same entity's table in
   both sets.
2. Use the next ordered migration filename following the repository's existing
   naming convention.
3. Update repository SQL and typed row mappings in the same change.
4. Prove that the applicable migration set creates a clean database from
   scratch.
5. For shard changes, verify that routing rejects shards whose schema version is
   behind the application-required version.

## Persistence contract testing

Dapper and handwritten SQL are runtime-bound, so module repository contracts
must be verified with integration tests against the actual supported database
engine. In-memory substitutes and mocked connections do not verify SQL syntax,
column names, types, nullability, constraints, or Dapper mappings.

Repository contract tests start from a clean database created by the DbUp
migrations and cover both directions of persistence:

```text
Domain entity -> write repository -> table -> read repository -> row entity
Table -> row entity -> write repository rehydration -> domain entity
```

Tests verify every persisted property, nullable columns, supported status or
enum values, inserts, updates, and domain rehydration. Each migration set must
also be tested by constructing its database from scratch.

Use this minimum test matrix:

| Behavior | Required coverage |
| --- | --- |
| Insert or update | Domain input is persisted with every column verified |
| Read | Seeded table row maps to every row-model property |
| Rehydration | Row maps back to the domain entity when returned by a write repository |
| Nullability | Every supported nullable value is covered |
| Status or enum | Every supported persisted value is covered |
| Constraints | Important unique, foreign-key, and nullability constraints are exercised |
| Migration | The relevant migration set builds a clean SQL Server database |
| Shard routing | The repository reaches only the assignment selected by `ShardKey` |

Generic reflection tests that compare row-property names with table-column names
are not required. Such tests duplicate mapping configuration and do not handle
legitimate SQL aliases, projections, or naming differences. Migration-backed
repository integration tests are the authoritative verification of the contract
between tables, row entities, and domain entities.

## Completion checklist

Before considering persistence work complete, verify every applicable item:

- [ ] Every module database operation goes through a repository interface.
- [ ] Handlers, domain entities, and business services contain no Dapper, SQL,
      connection strings, `DbConnection`, logical shard IDs, or shard-directory
      access.
- [ ] Every business/domain entity ID is `long`, with generation behind
      `IIdGenerator`.
- [ ] The entity is stored in exactly one database role: General or shard.
- [ ] A sharded entity uses a stable, immutable, deterministic,
      culture-invariant discriminator.
- [ ] Shard routing uses `ShardKey`; no repository selects or guesses a physical
      shard.
- [ ] `ShardAssignments` remains authoritative and credentials remain outside
      the directory tables.
- [ ] General repositories use `IGeneralConnectionFactory`; sharded repositories
      use `IShardConnectionFactory`.
- [ ] Every persisted entity has separate read and write repository contracts
      and implementations.
- [ ] Query handlers depend only on read repositories.
- [ ] Command handlers depend only on write repositories, including for
      command-side checks and locked reads.
- [ ] Every Dapper result maps to a typed row; no `dynamic` result is used.
- [ ] A purpose-specific projection row exists only when one of the documented
      criteria applies.
- [ ] Domain entities store related IDs and contain no navigation objects or row
      types.
- [ ] No transaction crosses physical database boundaries.
- [ ] General and shard migrations remain separate and the correct set builds a
      clean database.
- [ ] Repository contract tests cover SQL, mappings, persisted properties,
      nullability, statuses, constraints, and required rehydration.

If any applicable checkbox cannot be satisfied, do not silently work around the
rule. State the conflict and request an architecture decision.
