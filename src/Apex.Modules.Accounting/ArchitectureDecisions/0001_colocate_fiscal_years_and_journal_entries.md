# ADR 0001: Co-locate Fiscal Years and Journal Entries

- Status: Accepted
- Date: 2026-07-20

## Context

Reference-number allocation, provisional Journal Entry numbering, posting eligibility, daily
finalization, and final number freezing all depend on authoritative Fiscal Year state. Keeping the
Fiscal Year in the General database while Journal Entries reside in shards would make these
operations cross-database workflows without a single atomic transaction.

## Decision

Fiscal Years, Journal Entries, and Journal Entry Lines are authoritative in the shard selected by
`ShardKey("FiscalYear", fiscalYearId)`. Fiscal Year state and number counters are updated in the
same shard transaction as the Journal Entry operation that depends on them.

The Fiscal Year ID is the explicit routing discriminator. IDs of entries or books must not be used
to infer a shard.

## Consequences

- Number allocation and entry insertion can commit atomically.
- Fiscal Year locking can serialize posting, reversal, and daily finalization.
- Callers must know the Fiscal Year ID before accessing authoritative data.
- Cross-Fiscal-Year work is necessarily a coordinated multi-shard read or workflow.

## Rejected alternatives

- Keeping the authoritative Fiscal Year in the General database was rejected because it cannot
  atomically protect counters and Journal Entries in a shard.
- Distributed transactions were rejected because Apex does not permit cross-database transactions.

