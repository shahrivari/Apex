# ADR 0007: Enforce Gapless Fiscal Years Best Effort

- Status: Accepted
- Date: 2026-07-21

## Context

Fiscal Years for one Accounting Book must form a contiguous sequence. Their authoritative rows are
stored in independently routed Fiscal Year shards, while the General `fiscal_year_directory`
contains the only book-wide view. A strict cross-shard invariant would require different ownership
or a coordinated workflow with recovery state.

Fiscal Year definition changes are rare. The business accepts the narrow concurrency and stale-read
race rather than introducing that coordination complexity.

## Decision

Create, draft update, draft deletion, and cancellation check the General Fiscal Year directory before
committing the authoritative shard mutation. The candidate effective ranges, ordered by start date,
must be adjacent: each later start date is exactly one day after the preceding effective end date.

The first Fiscal Year for an Accounting Book may start on any date. Removing an edge Fiscal Year is
allowed because no gap remains between retained years. Removing a middle Fiscal Year or shortening
an effective range before a later Fiscal Year is rejected.

These checks are explicitly best effort. They do not make the directory authoritative and do not
create a General-to-shard transaction.

## Consequences

- Ordinary Fiscal Year mutations reject date gaps with a stable conflict error.
- Concurrent mutations or stale directory data can rarely violate the invariant.
- Directory repair remains the recovery mechanism for directory drift, but it does not automatically
  repair an invalid authoritative calendar.
- If strict enforcement becomes necessary, a later ADR must change ownership or define a recoverable
  coordinated workflow.

## Rejected alternatives

- Making the General directory authoritative was rejected because Fiscal Year lifecycle and counters
  remain shard-owned.
- Repartitioning all Fiscal Years by Accounting Book was rejected as disproportionate to the rare
  operation and accepted race.
- Distributed transactions were rejected because Apex prohibits cross-database transactions.

