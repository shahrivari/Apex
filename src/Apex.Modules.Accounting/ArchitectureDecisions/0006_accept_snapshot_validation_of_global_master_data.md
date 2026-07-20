# ADR 0006: Accept Snapshot Validation of Global Master Data

- Status: Accepted
- Date: 2026-07-20

## Context

Journal Entry commands validate global Accounting Book, Chart of Accounts, and Detail Account data
before committing authoritative Journal Entry changes in a Fiscal Year shard. Apex prohibits a
transaction spanning the General database and a shard, so global eligibility may change after it is
read but before the shard transaction commits.

## Decision

For the current phase, Accounting accepts this narrow race. Commands validate global master data as
a snapshot, then enforce all shard-owned invariants and mutations atomically inside the shard.

The implementation must not imply that the global validation and shard commit share one transaction.
If strict coordination becomes a requirement, a new ADR must define ownership or an explicit
cross-database workflow before implementation changes.

## Consequences

- Commands remain compatible with Apex transaction boundaries.
- A concurrently disabled global record can rarely be used by an already validated command.
- Stored account codes preserve the accounting fact even if master data later changes.
- Monitoring or compensating workflows may be added if the accepted race becomes operationally
  significant.

## Rejected alternatives

- A distributed General-to-shard transaction was rejected by the persistence architecture.
- Moving all global master data into every Fiscal Year shard was rejected because it would duplicate
  ownership and complicate global maintenance.

