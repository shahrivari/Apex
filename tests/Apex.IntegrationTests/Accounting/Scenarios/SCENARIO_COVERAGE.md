# Accounting Scenario Coverage

Traceability catalogue required by `apex_accounting_scenario_tests_implementation_plan.md` §10.
Every scenario identifier from spec §9 is listed. Status values:

- **Implemented** — a test explicitly proves the rule; test method + file given.
- **Pending** — not yet implemented; scheduled for a later phase per the spec's phased plan (§11).
- **Pending Product Capability** — the capability does not exist yet in production (no reachable
  code path / no endpoint / no second shard), so no test can prove it without expanding the
  business specification. Not in scope for this phase.

This document reflects the current implementation through the supported parts of Phases 1–5:
the original basic/lifecycle scenarios, reversals, statistical entries, daily finalization,
projection reconciliation/rebuild, supported multi-book isolation, idempotency, and coordinated
concurrency and multi-shard routing. The current scenario filter contains 163 tests. The remaining
pending entries are unrelated to shard topology or require product contracts not yet exposed.

## Canonical scenario (spec §8)

| Test method + file | Capability spec / rule | Status |
| --- | --- | --- |
| `PostThreeEntriesAcrossDates_ShouldProduceCanonicalClosingBalances` — `BalanceCalculationScenarios.cs` | `journal_entries.md` posting + balance rules; `chart_of_accounts.md` account paths | Implemented |

## A. Basic posting and reporting (`BASIC-001`–`BASIC-012`)

| ID | Test method + file | Capability spec / rule | Status | Justification |
| --- | --- | --- | --- | --- |
| BASIC-001 | `PostBalancedDraftEntry_ShouldProduceCorrectBalances` — `BasicPostingScenarios.cs` | `journal_entries.md` draft→post lifecycle | Implemented | |
| BASIC-002 | `DraftEntry_ShouldNotAffectFinancialBalanceOrProjections` — `BasicPostingScenarios.cs` | `journal_entries.md` draft has no financial effect | Implemented | |
| BASIC-003 | `MultipleEntriesOnSameDay_ShouldAggregateCorrectly` — `BasicPostingScenarios.cs` | `journal_entries.md` daily turnover aggregation | Implemented | |
| BASIC-004 | `PostEntriesAcrossSeveralDays_ShouldReturnCorrectClosingBalances` — `BasicPostingScenarios.cs` | `journal_entries.md` running balance | Implemented | |
| BASIC-005 | `EntryWithMoreThanTwoLines_ShouldPostWhenBalanced` — `BasicPostingScenarios.cs` | `journal_entries.md` multi-line balanced posting | Implemented | |
| BASIC-006 | `MultipleLinesToSameAccount_ShouldAggregateWithinOneEntry` — `BasicPostingScenarios.cs` | `journal_entries.md` line aggregation | Implemented | |
| BASIC-007 | `DifferentAccounts_ShouldRemainIndependentlyReportable` — `BasicPostingScenarios.cs` | `journal_entries.md` per-account reporting isolation | Implemented | |
| BASIC-008 | `TrialBalance_TotalDebitShouldEqualTotalCredit` — `BasicPostingScenarios.cs` | `journal_entries.md` trial balance invariant | Implemented | |
| BASIC-009 | `TransactionReport_ShouldReturnEntriesInDefinedAccountingOrder` — `BasicPostingScenarios.cs` | `journal_entries.md` transaction ordering (accounting date, registered at, reference number, row number) | Implemented | |
| BASIC-010 | `JournalEntryAudit_ShouldReturnExpectedHeaderLinesStatusNumbersAndTimestamps` — `BasicPostingScenarios.cs` | `journal_entries.md` audit contract | Implemented | |
| BASIC-011 | `PostingEntry_ShouldAffectAuthoritativeDataAndProjectionsExactlyOnce` — `BasicPostingScenarios.cs` | `journal_entries.md` posting atomicity (exactly-once effect) | Implemented | |
| BASIC-012 | `EmptyDateRangeFilter_ShouldReturnEmptyValidResultRatherThanFabricatedZeroRows` — `BasicPostingScenarios.cs` | `journal_entries.md` report contract for no-activity ranges | Implemented | |

## B. Balance boundaries and aggregation (`BAL-001`–`BAL-016`)

| ID | Test method + file | Capability spec / rule | Status | Justification |
| --- | --- | --- | --- | --- |
| BAL-001 | `BalanceBeforeFirstActivityDate_ShouldBeZero` — `BalanceCalculationScenarios.cs` | `journal_entries.md` balance boundary semantics | Implemented | |
| BAL-002 | `BalanceOnFirstActivityDate_ShouldIncludeThatDate` — `BalanceCalculationScenarios.cs` | same | Implemented | |
| BAL-003 | `BalanceBetweenActivityDates_ShouldCarryPriorMovement` — `BalanceCalculationScenarios.cs` | same | Implemented | |
| BAL-004 | `BalanceOnFinalActivityDate_ShouldIncludeAllMovementsThroughThatDate` — `BalanceCalculationScenarios.cs` | same | Implemented | |
| BAL-005 | `BalanceAfterFinalActivityDate_ShouldRemainUnchanged` — `BalanceCalculationScenarios.cs` | same | Implemented | |
| BAL-006 | `PeriodReport_ShouldSeparateOpeningBalanceFromInPeriodTurnover` — `BalanceCalculationScenarios.cs` | `journal_entries.md` trial balance opening/turnover split | Implemented | |
| BAL-007 | `DebitAndCreditTurnover_ShouldBeIndependentlyCorrect` — `BalanceCalculationScenarios.cs` | `journal_entries.md` turnover independence | Implemented | |
| BAL-008 | `NetBalance_ShouldUseDebitPositiveCreditNegativeSemantics` — `BalanceCalculationScenarios.cs` | `journal_entries.md` net balance sign convention | Implemented | |
| BAL-009 | `DocumentTypeFiltering_ShouldIncludeOnlyRequestedActivity` — `BalanceCalculationScenarios.cs` | `journal_entries.md` document-type exclusion filter | Implemented | |
| BAL-010 | `AccountClassAggregation_ShouldCombineDescendantAccounts` — `BalanceCalculationScenarios.cs` | `chart_of_accounts.md` hierarchy rollup | Implemented | |
| BAL-011 | `GeneralAccountAggregation_ShouldCombineSubsidiaryAccounts` — `BalanceCalculationScenarios.cs` | `chart_of_accounts.md` hierarchy rollup | Implemented | |
| BAL-012 | `SubsidiaryAccountResult_ShouldCombineDetailAccounts` — `BalanceCalculationScenarios.cs` | `detail_accounts.md` + `chart_of_accounts.md` grain rollup | Implemented | |
| BAL-013 | `DetailAccountFiltering_ShouldIsolateOneDetailCode` — `BalanceCalculationScenarios.cs` | `detail_accounts.md` grain isolation | Implemented | |
| BAL-014 | `Pagination_ShouldNotChangeTotalsOrOmitDuplicateTransactions` — `BalanceCalculationScenarios.cs` | `journal_entries.md` transaction report pagination | Implemented | |
| BAL-015 | `VeryLargeValidDecimalAmount_ShouldBeHandledWithoutOverflow` — `BalanceCalculationScenarios.cs` | `journal_entries.md` / schema `DECIMAL(19,4)` bound | Implemented | |
| BAL-016 | `FractionalAmountBeyondSchemaScale_ShouldRoundToSchemaScaleWithoutServerError` — `BalanceCalculationScenarios.cs` | `journal_entries.md` money policy vs. schema `DECIMAL(19,4)` | Implemented | No validation rejects a 5-decimal amount; SQL Server silently rounds it to the column scale (round-half-away-from-zero) on insert. The 201 Created response echoes the unrounded input, disagreeing with the persisted/re-read value — a discovered production defect documented in the final handoff, not fixed here (no unambiguous money-precision policy exists in the spec). |

## C. Journal Entry lifecycle and validation (`JE-001`–`JE-030`)

All tests in `JournalEntryLifecycleScenarios.cs` unless noted. Capability spec / rule column refers
to `journal_entries.md` and the exact handler/validator confirmed by reading source (see file-level
doc comment for the fully-worked-out mechanism).

| ID | Test method | Rule / error code | Status | Justification |
| --- | --- | --- | --- | --- |
| JE-001 | `CreatingEntryWithUnknownAccountingBook_ShouldBeRejected` | `JournalEntryActivityValidator` cross-book check → 422 `journal_entry_accounting_date_outside_fiscal_year` | Implemented | The book lookup always uses the fiscal year's real owning book, never the client-supplied id directly, so "unknown book" and "cross-book fiscal year" (JE-002) share the same code path/error. |
| JE-002 | `CreatingEntryWithFiscalYearFromAnotherBook_ShouldBeRejected` | same as above | Implemented | |
| JE-003 | `CreatingEntryInDraftFiscalYear_ShouldBeRejected` | 422 `journal_entry_fiscal_year_not_open` | Implemented | |
| JE-004 | — | — | **Pending Product Capability** | `FiscalYearStatus.Closed` is not reachable (see FY-011 decision, same reasoning). JE-003/JE-005 already prove creation is rejected for the two reachable non-Open statuses (Draft, Cancelled). |
| JE-005 | `CreatingEntryInCancelledFiscalYear_ShouldBeRejected` | 422 `journal_entry_fiscal_year_not_open` | Implemented | |
| JE-006 | `AccountingDateBeforeFiscalYearStart_ShouldBeRejected` | 422 `journal_entry_accounting_date_outside_fiscal_year` | Implemented | |
| JE-007 | `AccountingDateAfterEffectiveFiscalYearEnd_ShouldBeRejected` | same | Implemented | |
| JE-008 | `AccountingDateOnFinalizedThroughDate_ShouldBeRejected` | 422 `journal_entry_accounting_date_finalized` | Implemented | |
| JE-009 | `DraftCreationWithoutLines_ShouldBeRejected` | 400 `validation_failed` | Implemented | FluentValidation `Lines` `NotEmpty` fires before any domain check — not the domain `journal_entry_insufficient_lines` code. |
| JE-010 | `PostingWithFewerThanTwoLines_ShouldBeRejected` | 422 `journal_entry_insufficient_lines` | Implemented | Only reachable at Post (no lines-array body on Post; a 1-line draft is otherwise a valid Create). |
| JE-011 | `PostingUnbalancedEntry_ShouldBeRejectedAtomically` | 422 `journal_entry_unbalanced` | Implemented | Verified atomic via `AssertRejectedWithoutSideEffectsAsync` + re-fetch shows entry still `DRAFT`. |
| JE-012 | `ZeroAmountLine_ShouldBeRejected` | 400 `validation_failed` | Implemented | FluentValidation `Amount` `GreaterThan(0)` — domain `non_positive_amount` code unreachable via HTTP. |
| JE-013 | `NegativeAmountLine_ShouldBeRejected` | 400 `validation_failed` | Implemented | Same mechanism as JE-012. |
| JE-014 | `MissingEntryDescription_ShouldBeRejected` | 400 `validation_failed` | Implemented | FluentValidation `NotEmpty` — domain `description_required` unreachable via HTTP. |
| JE-015 | `MissingLineDescription_ShouldBeRejected` | 400 `validation_failed` | Implemented | Same mechanism. |
| JE-016 | `InvalidSide_ShouldBeRejected` | 422 `journal_entry_unsupported_side` | Implemented | Non-empty but unrecognized value; empty is a 400 instead. |
| JE-017 | `UnsupportedDocumentType_ShouldBeRejected` | 422 `journal_entry_unsupported_document_type` | Implemented | |
| JE-018 | `UnsupportedInsertionType_ShouldBeRejected` | 422 `journal_entry_unsupported_insertion_type` | Implemented | |
| JE-019 | `UnsupportedBalanceEffect_ShouldBeRejected` | 422 `journal_entry_unsupported_balance_effect` | Implemented | |
| JE-020 | `AppendingLineWithDuplicateRowNumber_ShouldBeRejected` | 422 `journal_entry_duplicate_row_number` | Implemented | Only reachable via Append — Create/Replace always discard client `RowNumber` and renumber contiguously from 1. |
| JE-021 | `DraftHeader_MayBeUpdatedOnAnUnfinalizedDate` | `PUT .../{fiscalYearId}/{id}` → 200 | Implemented | |
| JE-022 | `DraftLines_MayBeAppended` | `POST .../lines` → 200, contiguous max+1 numbering | Implemented | |
| JE-023 | `DraftLines_MayBeReplacedWithContiguousRowNumbering` | `PUT .../lines` → 200, renumbered 1..N | Implemented | |
| JE-024 | `EligibleDraft_MayBePhysicallyDeleted` | `DELETE` → 204, then 404 on Get | Implemented | |
| JE-025 | `PostedHeader_CannotBeEdited` | 422 `journal_entry_draft_required` | Implemented | |
| JE-026 | `PostedLines_CannotBeAppendedOrReplaced` | 422 `journal_entry_draft_required` (both append and replace) | Implemented | |
| JE-027 | `PostedEntry_CannotBeDeleted` | 422 `journal_entry_draft_required` | Implemented | Reuses `DraftRequired`, not a dedicated "posted immutable"/"cannot delete posted" code. |
| JE-028 | `MissingEntry_ShouldReturnSpecifiedNotFoundError` | 404 `journal_entry_not_found` | Implemented | |
| JE-029 | `FailedMutation_ShouldPreserveThePreviouslyCommittedDraftExactly` | 422 (rejected update) + field-level re-fetch proof | Implemented | |
| JE-030 | `PostingTheSameEntryTwice_ShouldFailWithoutDuplicatingProjectionMovement` | 422 `journal_entry_draft_required` | Implemented | Double-post is **not** idempotent and **not** 409 — confirmed via source: `PostJournalEntryHandler` calls `EnsureDraft()` first. Balance re-verified unchanged after the rejected second post. |

## D. Chart of Accounts and account paths (`PATH-001`–`PATH-015`)

All tests in `AccountPathScenarios.cs`. Capability spec / rule: `chart_of_accounts.md`. Key
mechanism (see file-level doc comment): draft creation/append/replace only checks path *existence*,
never *eligibility* (archived ancestors) — eligibility is re-checked only at Post.

| ID | Test method | Rule / error code | Status | Justification |
| --- | --- | --- | --- | --- |
| PATH-001 | `ActiveCompletePath_ShouldAcceptPosting` | green path | Implemented | |
| PATH-002 | `UnknownAccountClass_ShouldBeRejectedAtPosting` | 422 `journal_entry_invalid_account_code_path` | Implemented | Draft creation succeeds silently; rejection only surfaces at Post. |
| PATH-003 | `UnknownGeneralAccount_ShouldBeRejectedAtPosting` | same | Implemented | |
| PATH-004 | `UnknownSubsidiaryAccount_ShouldBeRejectedAtPosting` | same | Implemented | |
| PATH-005 | `GeneralAccountFromAnotherAccountClass_ShouldBeRejectedAtPosting` | same | Implemented | The Class→General→Subsidiary join requires exact parent-id chaining, so a general belonging to a different class cannot resolve — indistinguishable from "unknown general" at the error-code level. |
| PATH-006 | `SubsidiaryAccountFromAnotherGeneralAccount_ShouldBeRejectedAtPosting` | same | Implemented | Same reasoning, one level down. |
| PATH-007 | `OnlyAGenuineSubsidiaryRow_ShouldBePostable` | 422 `journal_entry_invalid_account_code_path` | Implemented (adapted) | As literally worded ("only a posting-level Subsidiary Account can receive activity") this is not independently reachable: `JournalEntryLineRequest` always requires all three codes, so there is no request shape that targets a General/Class account directly — the rule is structurally guaranteed by the request DTO, not a runtime check. The adapted test proves the underlying mechanism instead: a code triple built from two genuinely existing ancestor codes but a non-existent Subsidiary code still fails path resolution exactly like any unknown code — there is no "posting at a higher level" fallback. |
| PATH-008 | `ArchivedSubsidiaryAccount_ShouldRejectNewPosting` | 422 `journal_entry_account_not_eligible` | Implemented | |
| PATH-009 | `ArchivedGeneralAccount_ShouldPreventPostingThroughDescendants` | same | Implemented (adapted) | `PostingEligible` is a single boolean folding Class/General/Subsidiary status — archived-General and archived-Subsidiary are indistinguishable by error code. Also, Chart of Accounts requires a parent to have no active children before archiving, so archiving the General first requires archiving its Subsidiary — there is no reachable state with an archived General and an active Subsidiary beneath it. The test archives bottom-up (documented in code) and proves the resulting rejection. |
| PATH-010 | `ArchivedAccountClass_ShouldPreventPostingThroughDescendants` | same | Implemented (adapted) | Same reasoning one level up (Subsidiary → General → Class, in that order). |
| PATH-011 | `ExistingHistory_ShouldRemainReportableAfterAccountArchival` | balances/trial balance unaffected by archival | Implemented | |
| PATH-012 | `AccountRename_ShouldChangeDisplayNotStoredLines` | rename via `PUT .../subsidiary-accounts/{id}` (Name-only DTO) | Implemented | |
| PATH-013 | `AccountCodes_ShouldNotBeChangeable` | structural: update DTO has no Code field | Implemented | |
| PATH-014 | `AccountNatureAndParent_ShouldNotBeChangeable` | structural: update DTO has no Nature/ParentId field | Implemented | |
| PATH-015 | `ReactivatedValidPath_ShouldAcceptPostingAgain` | reactivate Class→General→Subsidiary (reverse order), posting succeeds | Implemented | |

## E. Detail Accounts (`DETAIL-001`–`DETAIL-014`)

All tests in `DetailAccountScenarios.cs`. Capability spec / rule: `detail_accounts.md` +
`chart_of_accounts.md` (Subsidiary Account detail-type requirement). Key mechanism (see file-level
doc comment): when a line's account path already resolves, the Detail-Account requirement is
validated at DRAFT CREATION time (via `ValidateDetailAccountForPostingHandler`), not only at
posting — most tests exercise `CreateDraftEntryAsync` directly.

| ID | Test method | Rule / error code | Status | Justification |
| --- | --- | --- | --- | --- |
| DETAIL-001 | — | (retired) | Removed | A Detail Account is now mandatory on every line; the "no detail account required" (`NONE`) requirement was removed, so a line without a Detail Account is no longer a valid case to cover. |
| DETAIL-002 | — | (retired) | Removed | Same removal of `NONE`: `detail_account_not_allowed` no longer exists — a supplied Detail Account is never rejected for being "not allowed". |
| DETAIL-003 | `BankRequirement_ShouldAcceptAnActiveBankDetailAccount` | green path | Implemented | |
| DETAIL-004 | `BankRequirement_ShouldRejectPersonAndSymbolDetailAccounts` | 422 `detail_account_type_mismatch` (×2) | Implemented | |
| DETAIL-005 | `PersonRequirement_ShouldAcceptPersonAndRejectBankAndSymbol` | green + 422 `detail_account_type_mismatch` (×2) | Implemented | |
| DETAIL-006 | `SymbolRequirement_ShouldAcceptSymbolAndRejectBankAndPerson` | green + 422 `detail_account_type_mismatch` (×2) | Implemented | |
| DETAIL-007 | `MissingRequiredDetailAccount_ShouldBeRejected` | 422 `detail_account_required` | Implemented | |
| DETAIL-008 | `UnknownDetailAccountCode_ShouldBeRejected` | **404** `detail_account_not_found` | Implemented | A 404 returned from inside a draft-creation `POST` — confirmed by reading `ValidateDetailAccountForPostingHandler` (throws `NotFoundException`), not assumed. |
| DETAIL-009 | `ArchivedDetailAccount_ShouldBeRejectedForNewPosting` | 422 `detail_account_archived` | Implemented | |
| DETAIL-010 | `HistoricalBalances_ShouldRemainAvailableAfterDetailAccountArchival` | balance unaffected by archival | Implemented | |
| DETAIL-011 | `RenamingDetailAccount_ShouldChangeDisplayNotStoredLinesOrBalances` | rename via `PUT` (Name only supplied) | Implemented | |
| DETAIL-012 | `ChangingDetailAccountType_ShouldAffectFutureEligibilityNotHistory` | retype → future posting 422 `detail_account_type_mismatch`; prior balance unchanged | Implemented | |
| DETAIL-013 | `TwoDetailCodesUnderOneSubsidiaryAccount_ShouldRemainSeparatelyReportable` | turnover isolated per detail code | Implemented | |
| DETAIL-014 | `DetailAccountCode_ShouldBeImmutable` | 422 `detail_account_code_immutable` | Implemented | |

## F. Fiscal Year controls (`FY-001`–`FY-019`)

All tests in `FiscalYearControlScenarios.cs`. Capability spec / rule: `fiscal_years.md`. **Two
catalogue items were adapted** after reading the authoritative spec and current code (see the
file-level doc comment for full reasoning):

- **FY-006** ("Gaps between Fiscal Years are allowed") contradicts `fiscal_years.md` invariant 5
  ("must form a contiguous sequence without date gaps") and the current `CreateFiscalYearHandler`
  (which rejects any creation that would leave a gap, 409 `fiscal_year_dates_have_gap` — matching
  the recent commit "enforce contiguous fiscal years"). The capability spec is authoritative over
  the catalogue's shorthand description, so FY-006 here proves the real, current rule: **creating**
  **a fiscal year with a gap is rejected**, not allowed.
- **FY-016** ("Resolution inside an allowed gap") — since gaps between *created* years are now
  rejected, the only legitimate gap is the trailing period after a Fiscal Year is cancelled before
  its original end date. FY-016 exercises resolution for a date in that trailing gap.

| ID | Test method | Rule / error code | Status | Justification |
| --- | --- | --- | --- | --- |
| FY-001 | `DraftFiscalYear_ShouldAcceptNoAccountingActivity` | 422 `journal_entry_fiscal_year_not_open` | Implemented | |
| FY-002 | `OpeningFiscalYear_ShouldEnableEligibleActivity` | green path | Implemented | |
| FY-003 | `AtMostOneFiscalYearMayBeOpenPerBook` | 409 `fiscal_year_open_already_exists` | Implemented | Also verifies the first year remains `OPEN` and the second remains `DRAFT` afterward. |
| FY-004 | `DifferentAccountingBooks_MayEachHaveAnOpenFiscalYear` | green path, two books | Implemented | |
| FY-005 | `OverlappingFiscalYearsInOneBook_ShouldBeRejected` | 409 `fiscal_year_dates_overlap` | Implemented | |
| FY-006 | `CreatingFiscalYearWithGapFromExistingRange_ShouldBeRejected` | 409 `fiscal_year_dates_have_gap` | Implemented (adapted — see above) | |
| FY-007 | `DraftDatesAndTitle_MayBeChanged` | `PUT` → 200 | Implemented | |
| FY-008 | `OpenFiscalYearDates_CannotBeChanged` | 422 `fiscal_year_cannot_be_updated` | Implemented | Whole request rejected, not just dates — no partial update path exists. |
| FY-009 | `EligibleUnusedDraftFiscalYear_MayBeDeleted` | `DELETE` → 204 | Implemented | Deletes the trailing (edge) year specifically, since deleting an interior year would itself introduce a gap. |
| FY-010 | `OpenFiscalYear_CannotBeDeleted` | 422 `fiscal_year_cannot_be_deleted` | Implemented | |
| FY-011 | — | — | **Pending Product Capability** | `FiscalYearStatus.Closed` and `ClosedAt` exist in the domain model but no code path (`Open()`/`Cancel()`) ever transitions to `Closed`, and no `/close` HTTP route exists. Only `Cancelled` is a reachable terminal state today (see FY-012). Do not add a `Close()` transition or endpoint — that would silently expand the business specification. |
| FY-012 | `CancelledFiscalYear_ShouldBeTerminal` | 422 on Open/Update/Cancel/Delete attempts (`CannotBeOpened`/`CannotBeUpdated`/`CannotBeCancelled`/`CannotBeDeleted`) | Implemented | The real reachable terminal-state proof. |
| FY-013 | `CancellationIsValidOnlyAtTheFinalizedBoundary` | 422 `fiscal_year_cannot_be_cancelled` for wrong date, success at the exact boundary | Implemented | Confirmed via `FiscalYear.Cancel()`: `CancellationDate` must equal `FinalizedThroughDate` exactly. |
| FY-014 | `TerminalFiscalYears_ShouldRemainHistoricallyReportable` | balance/audit/status still queryable after cancellation | Implemented | |
| FY-015 | `FiscalYearResolution_ShouldSelectTheUniqueMatchingYear` | `GET /resolve` → 200, correct year per date | Implemented | |
| FY-016 | `ResolutionInTheGapAfterCancellation_ShouldReturnTheDocumentedNotFoundOutcome` | 404 `fiscal_year_not_found_for_date` | Implemented (adapted — see above) | |
| FY-017 | `OpeningOneFiscalYear_ShouldNotSilentlyCloseAnother` | 409 `fiscal_year_open_already_exists`, first year still `OPEN` afterward | Implemented | |
| FY-018 | `ArchivedBook_CannotReceiveANewFiscalYear` | 422 `fiscal_year_accounting_book_archived` | Implemented | |
| FY-019 | `OnlyAnActiveBook_PermitsFiscalYearOpening` | 422 `fiscal_year_accounting_book_not_active` (Draft book and Suspended book) | Implemented | |

## G. Reversals (`REV-001`–`REV-015`)

| ID | Status | Justification |
| --- | --- | --- |
| REV-001 – REV-015 | Implemented | `ReversalScenarios.cs`; each identifier has an explicit test and authoritative/projection assertions where applicable. |

## H. Statistical entries (`STAT-001`–`STAT-008`)

| ID | Status | Justification |
| --- | --- | --- |
| STAT-001 – STAT-008 | Implemented | `StatisticalEntryScenarios.cs`; statistical history is verified while financial projections remain unchanged. |

## I. Numbering and daily finalization (`NUM-001`–`NUM-019`)

| ID | Status | Justification |
| --- | --- | --- |
| NUM-001 – NUM-019 | Implemented | `DailyFinalizationScenarios.cs` and `ConcurrencyScenarios.cs`; NUM-019 is proven by the coordinated allocation scenario. |

## J. Projection integrity and repair (`PROJ-001`–`PROJ-015`)

| ID | Status | Justification |
| --- | --- | --- |
| PROJ-001 – PROJ-014 | Implemented | `ProjectionIntegrityScenarios.cs` plus tagged posting, reversal, statistical, and concurrency scenarios. |
| PROJ-015 | **Pending Product Capability** | Projection version/update-timestamp semantics are persisted, but no stable public contract defines their expected externally observable behavior. |

## K. Multiple books, Fiscal Years, and shards (`MULTI-001`–`MULTI-010`)

| ID | Status | Justification |
| --- | --- | --- |
| MULTI-001 – MULTI-003 | Implemented | `AccountingBookIsolationScenarios.cs`; balances, projections, and number sequences are isolated. |
| MULTI-004 | Implemented | `MultiFiscalYearScenarios.CrossFiscalYearTurnover_ShouldAggregateOnlyTheRequestedPhysicalPartitions`. |
| MULTI-005 | Implemented | Same scenario verifies Fiscal-Year-specific and full-range filtering. |
| MULTI-006 | Implemented | `MultiFiscalYearScenarios.ReversalRequestThroughAnotherFiscalYear_ShouldNotCrossTheShardBoundary`. |
| MULTI-007 | Implemented | `MultiFiscalYearScenarios.CrossFiscalYearReport_ShouldFailClosedWhenARequiredShardIsUnavailable`. |
| MULTI-008 | Implemented | Same failure scenario asserts no successful partial response is returned. |
| MULTI-009 | Implemented | Cross-shard scenario verifies assignments and authoritative physical placement. |
| MULTI-010 | Implemented | `MultiFiscalYearScenarios.RepairingTheFiscalYearDirectory_ShouldRestoreDiscoveryWithoutChangingShardData`. |

## L. Idempotency (`IDEM-001`–`IDEM-008`)

| ID | Status | Justification |
| --- | --- | --- |
| IDEM-001 – IDEM-004, IDEM-008 | Implemented | `IdempotencyScenarios.cs`; same-source replay, conflicting payloads, source-type scope, and coordinated duplicate requests are covered. |
| IDEM-005 | Implemented | `IdempotencyScenarios.SameSourcePairInADifferentFiscalYear_ShouldRemainIndependentlyEligible`. The unique index is `(fiscal_year_id, source_type, source_reference)` (`000001_create_journal_entry.sql`, `ux_journal_entry_source`) and rule 42 in `journal_entries.md` ("...identifies at most one Journal Entry **within a Fiscal Year**") confirms uniqueness is fiscal-year-scoped, not book-scoped — the same source pair in a second Fiscal Year of the same book must not collide, and does not. |
| IDEM-006 | Implemented | `IdempotencyScenarios.FailedFirstAttempt_ShouldNotPreventALaterValidAttemptWithTheSameSource`. A first attempt with the same source pair is rejected by line-level validation (negative amount, 400) before any row is written; a corrected retry with the identical source pair succeeds normally, proving no committed idempotency record blocks the later valid attempt. |
| IDEM-007 | **Not Applicable** | `journal_entries.md` §19 states trusted Source Type/Source Reference are "accepted only from trusted internal or migration interfaces," and no runtime check in `CreateDraftJournalEntryValidator`/`CreateDraftJournalEntryHandler` currently restricts which Insertion Type may supply them. Raised with the product owner: manual entries supplying Source Type/Source Reference are explicitly permitted in this system by product decision, so no restriction exists to test and none should be added. The capability doc's wording describes typical usage guidance, not an enforced runtime rule. |

## M. Concurrency (`CON-001`–`CON-008`)

| ID | Status | Justification |
| --- | --- | --- |
| CON-001 – CON-005, CON-008 | Implemented | `ConcurrencyScenarios.cs` and `IdempotencyScenarios.cs`, using a gate and no timing delays. |
| CON-006 | Implemented | `ConcurrencyScenarios.ConcurrentReversalAttempts_ShouldCreateExactlyOneReversal`. Five concurrent reversal attempts against the same posted entry, released together via a countdown-gate barrier; `ReverseJournalEntryHandler` locks the original entry row (`GetByReferenceNumberForUpdateAsync`) inside its shard transaction, so exactly one commits and the rest observe `ReversedByReferenceNumber` already set and fail with 409 `journal_entry_already_reversed`. |
| CON-007 | Implemented | `ConcurrencyScenarios.ConcurrentPostingAndFinalization_ShouldNeverLeavePartialOrInconsistentNumbering`. Three concurrent `Post` calls plus one concurrent `Finalize` call on the same boundary date, released together via a barrier. `PostJournalEntryHandler` and `FinalizeFiscalYearHandler` both lock the same `fiscal_year` row (`WITH (UPDLOCK, ROWLOCK)` in `FiscalYearWriteRepository.GetByIdForUpdateAsync`), fully serializing the two operation types. The test asserts the outcome is always one of exactly two consistent states — finalize succeeds and every race-date entry ends up Posted and Number-Finalized, or finalize fails with 409 `journal_entry_drafts_block_finalization` and the finalized-through boundary is unchanged — never a partial mix. |
