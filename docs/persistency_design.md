# Persistence Design

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

Every entity primary key is a `long`, stored as SQL Server `bigint` and generated
through the configured TSID library. TSID is only an implementation of
`IIdGenerator`; domain models and repository contracts contain only `long` IDs.

## Data placement

General entities live in the General Database. Sharded entities live in exactly
one shard database. Entities contain scalar values, value objects, and related
entity IDs; navigation objects are forbidden.

Database foreign keys are valid only when both rows always live in the same
physical database. Cross-database references are logical IDs.

## Shard keys

```csharp
public readonly record struct ShardKey(
    string EntityType,
    string Discriminator);
```

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

Repositories construct shard keys explicitly. Attributes and reflection do not
discover discriminators.

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

`IShardConnectionFactory` resolves `ShardAssignments`, verifies assignment and
shard status and schema version, resolves the allow-listed connection name, and
opens the provider connection. Repositories never select a physical shard.

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
separate detail and summary rows just because two queries select different column
subsets; prefer selecting the complete normal row when the table is reasonably
small.

Add another row type only when the query shape is materially different, such as:

- a join across multiple entities;
- an aggregate or report;
- a denormalized projection;
- calculated columns that do not belong to the normal entity row;
- a large table where selecting the full row would be measurably wasteful.

Name such types after their purpose, for example `AccountingBookUsageReportRow`,
not as another generic copy of the entity. Row entities contain no business
behavior. Domain entities never depend on row entities, and row entities are
never returned directly as HTTP responses.

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

Generic reflection tests that compare row-property names with table-column names
are not required. Such tests duplicate mapping configuration and do not handle
legitimate SQL aliases, projections, or naming differences. Migration-backed
repository integration tests are the authoritative verification of the contract
between tables, row entities, and domain entities.

## Required rules

1. All module database access goes through repository interfaces.
2. Business code never handles database or shard infrastructure.
3. Entity IDs are `long`; TSID remains behind `IIdGenerator`.
4. Routing uses `ShardKey(EntityType, Discriminator)`.
5. `ShardAssignments` is authoritative; routing never guesses placement.
6. Credentials remain outside the General Database directory.
7. General repositories use `IGeneralConnectionFactory`.
8. Sharded repositories use `IShardConnectionFactory`.
9. Dapper reads use typed row models; `dynamic` is forbidden.
10. Entities store related IDs, never navigation objects.
11. Transactions remain within one physical database.
12. General and shard migrations remain separate.
13. Every persisted module entity has separate read and write repository
    interfaces and implementations.
14. Query handlers use read repositories; command handlers use write repositories.
15. Command-side checks and locked reads stay in the write repository transaction.
16. Handlers and business services depend on repository interfaces, not concrete
    repository implementations.
17. Every module repository has migration-backed integration tests against the
    supported database engine, including write/read round trips and row-to-domain
    rehydration where the repository returns domain entities.
