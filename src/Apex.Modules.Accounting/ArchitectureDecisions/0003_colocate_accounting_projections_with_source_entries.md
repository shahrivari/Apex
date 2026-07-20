# ADR 0003: Co-locate Accounting Projections with Source Entries

- Status: Accepted
- Date: 2026-07-20

## Context

Daily turnover and balance projections are updated when a financial Journal Entry is posted or
reversed. Committing source entries and projections in different databases could expose missing or
duplicated financial effects after a partial failure.

## Decision

Daily Account Turnover and Daily Account Balance projections reside in the same Fiscal Year shard
as their source Journal Entries and Lines. Posting or reversal and its complete projection effect
commit in one shard transaction.

Journal Entries and Lines remain the only accounting source of truth. Projections are rebuildable
read models and may not accept independent business writes. Rebuilds serialize with online writes
using the authoritative Fiscal Year lock.

## Consequences

- A posted entry cannot commit without its projection effects.
- Projection corruption can be detected and repaired from source truth.
- Cross-Fiscal-Year aggregate reporting must read more than one shard.
- Projection schema changes require controlled rebuilds.

## Rejected alternatives

- General-database projections were rejected because posting would cross a database boundary.
- Asynchronous projection updates were rejected for current accounting reports because they would
  expose eventual consistency immediately after posting.

