# Accounting Architecture Decision Records

This directory records durable architectural decisions owned by the Accounting module. Capability
specifications remain authoritative for business behavior; these records explain module-level data
placement, consistency, transaction, and reporting choices.

Accepted records are immutable. A changed decision must be superseded by a new record rather than
rewriting the original rationale.

| ADR | Status | Decision |
| --- | --- | --- |
| [0001](0001_colocate_fiscal_years_and_journal_entries.md) | Accepted | Co-locate Fiscal Years and Journal Entries in one shard |
| [0002](0002_use_general_fiscal_year_discovery_directory.md) | Accepted | Use an eventually consistent General fiscal-year discovery directory |
| [0003](0003_colocate_accounting_projections_with_source_entries.md) | Accepted | Co-locate accounting projections with their source entries |
| [0004](0004_use_all_or_nothing_cross_fiscal_year_reports.md) | Accepted | Use all-or-nothing cross-Fiscal-Year reporting |
| [0005](0005_restrict_reversals_to_the_original_fiscal_year.md) | Accepted | Restrict reversals to the original Fiscal Year |
| [0006](0006_accept_snapshot_validation_of_global_master_data.md) | Accepted | Accept snapshot validation of global master data |
| [0007](0007_enforce_gapless_fiscal_years_best_effort.md) | Accepted | Enforce gapless Fiscal Years using best-effort directory checks |

