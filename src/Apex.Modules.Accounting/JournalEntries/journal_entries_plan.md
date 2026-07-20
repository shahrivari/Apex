# Journal Entries — Implementation Plan & Status

Status of the Journal Entries capability, delivered in verified increments. The normative
requirements live in `journal_entries.md`; this file tracks execution progress.

Last updated: 2026-07-20.

## Key architectural decisions (locked)

The authoritative rationale and consequences for these decisions are recorded in the Accounting
module's [`ArchitectureDecisions`](../ArchitectureDecisions/README.md) directory.

- **Sharded by fiscal year**: `ShardKey("FiscalYear", fiscalYearId)`. Reads require the
  `fiscalYearId` to route (e.g. `GET /{fiscalYearId}/{id}`). First entity in the codebase to use
  the sharding infrastructure.
- **Fiscal Year is authoritative in the shard**: its lifecycle/finalization state and both number
  counters are co-located with Journal Entries. Draft creation allocates Reference Number and
  provisional Journal Entry Number in the same transaction that inserts the entry.
- **General discovery directory**: `fiscal_year_directory` is an eventually consistent list/date
  resolution index. Synchronization is synchronous best-effort and an explicit repair endpoint can
  rebuild a row after failure.
- **Projections are shard-resident** so posting + projection updates commit atomically.
- **Shard assignments** are provisioned lazily on first use (`IShardAssignmentProvisioner`);
  physical shard catalog entries are operational configuration, and tests seed their own catalog.

## Done

### Increment 1 — Foundation + draft lifecycle ✅
- Fiscal Year shard routing, authoritative repository, and General discovery directory.
- ChartOfAccounts `IAccountPathResolver` (public account-code-path resolution).
- Sharded domain (`JournalEntry`, `JournalEntryLine`, enums, errors) + shard tables
  (`Scripts/Shard/000001`) + shard-key factory + shard read/write repositories.
- Draft use cases: create, get (id / reference# / journal-entry#), search, update header,
  append lines, replace lines, delete.
- Tests: domain unit + shard repository-contract + handler + HTTP integration (all green).

### Increment 2 — Posting + projections + idempotency ✅
- `PostJournalEntry` with full revalidation (draft, fiscal year open/not-finalized, account paths
  exist + eligible, detail accounts, ≥2 lines, debit = credit); atomic DRAFT→POSTED.
- Shard projections `daily_account_turnover` + `daily_account_balance` (`Scripts/Shard/000002`),
  updated atomically with posting; FINANCIAL only, statistical excluded.
- Source-Reference idempotency on create (equivalent replay returns existing; divergent → conflict).
- Tests: domain `Post` + posting/projection/statistical/idempotency integration (all green).

### Increment 3a — Reversal ✅
- **Reverse posted entry** (§11.7): implemented as a single Fiscal Year shard transaction. The new
  posted entry receives both numbers, copies every line with debit/credit swapped, links both
  entries, and applies opposite financial projection effects on its own accounting date.
- Reversal is restricted to the original Fiscal Year and enforced exactly once by row locking,
  aggregate rules, and a filtered unique index.
- Statistical reversals do not modify financial projections.

### Increment 3b — Daily finalization ✅
- **Finalize accounting day** (§11.10): implemented as one Fiscal Year shard transaction with an
  exactly-next-day boundary, blocking-draft detection, source-to-projection reconciliation,
  deterministic unfinalized-tail renumbering, number freezing, and boundary advancement.
- Collision-safe two-phase renumbering preserves Journal Entry Number uniqueness throughout the
  transaction while leaving later numbers provisional.
- Fiscal Year-first locking serializes finalization with create/edit/post/delete/reverse operations.

### Increment 4 — Reporting + reconciliation ✅
- Full-Fiscal-Year and unfinalized-suffix projection rebuilds run atomically under the Fiscal Year
  lock; explicit reconciliation reports grain-level expected and actual values without mutation.
- Projection-backed reports cover trial balance, balance as of date, Document-Type-filtered
  turnover, and cross-Fiscal-Year period turnover.
- Cross-Fiscal-Year turnover requires explicit Fiscal Year IDs, reads shards concurrently, merges
  deterministically, and fails the whole request if any shard read fails.
- Source-backed general-ledger and journal reports preserve entry/line ordering, descriptions,
  numbering, source dimensions, and reversal links; drill-down and audit history are exposed
  separately.
- Sparse per-date balance deltas remain the chosen storage strategy; closing balances are summed
  at read time and rebuild/reconciliation operate on the same grain.

## Known limitations to revisit

- Source-Reference uniqueness is intentionally scoped to one Fiscal Year.
- Reversals are restricted to the original Fiscal Year, so reversal posting never crosses shards.
- Operation-specific authorization and Accounting-Book ownership authorization are deferred.
- General master-data eligibility is validated as a snapshot before the shard commit; the narrow
  cross-database race is accepted for the current phase.
- `daily_account_balance` uses sparse per-date deltas (closing computed on read); a future
  dense/checkpoint optimization remains optional and does not change report contracts.
- Shard assignment uses a single lowest-id active shard; a balanced provisioning workflow is future
  work.
