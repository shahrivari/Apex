# ADR 0004: Use All-or-Nothing Cross-Fiscal-Year Reports

- Status: Accepted
- Date: 2026-07-20

## Context

A report period can span Fiscal Years stored in different shards. Returning data from only the
reachable shards would produce plausible but financially incomplete totals.

## Decision

Cross-Fiscal-Year report requests provide the complete, explicit set of Fiscal Year IDs. The module
may read their shards concurrently, but any routing or read failure fails the entire request. No
partial report is returned.

After all reads succeed, results are merged and ordered deterministically. Period reports aggregate
turnover across boundaries and must not sum Fiscal Year closing balances or double-count opening
activity.

## Consequences

- Consumers never mistake a partial result for a complete financial report.
- Concurrent shard reads reduce fan-out latency without weakening failure semantics.
- One unavailable shard makes the complete report unavailable.
- Retries restart the logical report; no distributed read snapshot is claimed across shards.

## Rejected alternatives

- Best-effort partial results were rejected because omission is unsafe for accounting totals.
- A distributed transaction or distributed snapshot was rejected as unsupported and unnecessary for
  the accepted reporting consistency model.

