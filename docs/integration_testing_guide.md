# Integration Testing Guide

## Test layers

Use the smallest test that proves the behavior:

| Behavior | Test layer |
|---|---|
| Domain invariants and key normalization | Unit test |
| Handler transactions and repository SQL | Handler integration test |
| HTTP binding and error responses | HTTP integration test |
| General/shard routing and provisioning | Infrastructure integration test |

## Database topology

Business integration tests use one SQL Server general database. Sharding
integration tests use one general database and at least two shard databases.

The general connection is configured as:

```json
{
  "Sharding": {
    "GeneralConnectionStringName": "GeneralDb",
    "RequiredSchemaVersion": "1"
  },
  "ConnectionStrings": {
    "GeneralDb": "...",
    "ShardOne": "...",
    "ShardTwo": "..."
  }
}
```

Connection names stored in `shard` must exist under `ConnectionStrings`; tests
must never store raw credentials in directory rows.

## Migrations

Production migrations are separated by database role:

```text
tools/Apex.DatabaseMigrator/Scripts/General/
tools/Apex.DatabaseMigrator/Scripts/Shard/
```

Fixtures run:

```csharp
DatabaseMigrationRunner.RunGeneralMigrations(generalConnectionString);
DatabaseMigrationRunner.RunShardMigrations(shardConnectionString);
```

Test-only support tables remain under `Scripts.Test/`. Production tables must
never be created directly by test setup code.

## General database tests

Resolve the scoped `IGeneralConnectionFactory` for general-database access. Queries call
`OpenAsync`; commands execute through `IGeneralTransactionRunner` using the same
session and its transaction.

Tests should verify commit and rollback behavior and reset tables between tests.
Do not use one shared connection across parallel test cases.

## Sharding tests

At minimum verify:

- `ShardKey` creates a canonical `ShardKey`;
- only `ACTIVE` assignments are routed;
- `ACTIVE` and `DRAINING` shards can serve existing assignments;
- schema-behind, suspended, failed, and unknown connections fail closed;
- two partitions assigned to different shards observe different physical data;
- provisioning is idempotent and concurrent creation produces one assignment;
- initialization failure becomes `FAILED`;
- committed initialization left `PENDING` is activated by reconciliation;
- one `IShardTransactionRunner` invocation uses exactly one shard.

Use fixed table names in shard repositories. Never create year-suffixed tables or
derive SQL identifiers from a discriminator.

## Running tests

```bash
dotnet test tests/Apex.UnitTests/Apex.UnitTests.csproj
dotnet test tests/Apex.IntegrationTests/Apex.IntegrationTests.csproj
```

The integration suite requires Docker for its SQL Server container. A failing
container, migration, or readiness check must fail the fixture rather than skip
database assertions.
