# ADR 0002: Use a General Fiscal Year Discovery Directory

- Status: Accepted
- Date: 2026-07-20

## Context

Fiscal Year data is authoritative in shards, but discovery by Accounting Book and date must occur
before a shard key is known. Searching every shard would be expensive and would make routine
routing dependent on broad fan-out queries.

## Decision

The General database contains `fiscal_year_directory`, a non-authoritative discovery index used to
resolve candidate Fiscal Year IDs. Synchronization after authoritative shard changes is synchronous
best-effort. An explicit repair operation can rebuild a directory row from shard truth.

Routing failures fail closed. The directory never becomes authority for Fiscal Year lifecycle,
finalization state, or number counters.

## Consequences

- Date and book discovery remains a General-database lookup.
- A narrow inconsistency window is accepted between the shard commit and directory synchronization.
- Commands must validate authoritative Fiscal Year state again in the shard.
- Repair is an operationally visible and testable workflow.

## Rejected alternatives

- Fan-out discovery across all shards was rejected because it is costly and operationally fragile.
- Making the General row authoritative was rejected because it would recreate cross-database
  command transactions.

