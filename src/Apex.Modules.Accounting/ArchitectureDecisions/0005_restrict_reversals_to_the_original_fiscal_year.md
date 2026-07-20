# ADR 0005: Restrict Reversals to the Original Fiscal Year

- Status: Accepted
- Date: 2026-07-20

## Context

A reversal creates a posted entry, allocates numbers, links it to the original, and applies opposite
projection effects. Allowing the reversal in another Fiscal Year would split one invariant across
two shards.

## Decision

A Journal Entry may be reversed only inside its original Fiscal Year. The original entry, reversal,
number allocation, uniqueness protection, and projection effects commit in one shard transaction.

## Consequences

- Exactly-once reversal is protected by local locks and constraints.
- Reversal never requires cross-shard coordination.
- Corrections that belong to a later Fiscal Year require a separate accounting workflow and are not
  modeled as reversal of the earlier entry.

## Rejected alternatives

- Cross-Fiscal-Year reversal was rejected because it cannot atomically protect the original entry,
  new entry, and projections across shards.

