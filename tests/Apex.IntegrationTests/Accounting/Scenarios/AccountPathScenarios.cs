using System.Net;
using Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using static Apex.IntegrationTests.Accounting.Scenarios.Infrastructure.AccountingScenario;

namespace Apex.IntegrationTests.Accounting.Scenarios;

/// <summary>
/// Covers the required scenario catalogue group D ("Chart of Accounts and account paths",
/// PATH-001 through PATH-015 — spec §9.D).
///
/// Key mechanism, confirmed by reading <c>AccountPathReadRepository.ResolveAsync</c> and
/// <c>JournalEntryLineAssembler</c>/<c>PostJournalEntryHandler</c>: a line's account-code path is
/// resolved by an inner join Class→General→Subsidiary on exact codes. Draft creation/append/replace
/// only checks whether the path <em>exists</em> — and only runs the detail-account check when it
/// does; it never checks eligibility (archived ancestors). Full existence AND eligibility
/// (<c>PostingEligible = ClassStatus=ACTIVE &amp;&amp; GeneralStatus=ACTIVE &amp;&amp; SubsidiaryStatus=ACTIVE</c>)
/// are re-checked only at <c>POST /{fiscalYearId}/{id}/post</c>. Consequently:
///
/// - PATH-002 through PATH-007 (unknown/cross-parent codes) can only be observed by posting a draft
///   that was itself accepted at creation time (the invalid path is silently stored, then rejected
///   at Post as <c>journal_entry_invalid_account_code_path</c>).
/// - PATH-007 as literally worded ("only a posting-level Subsidiary Account can receive activity")
///   is not independently reachable: <c>JournalEntryLineRequest</c> always requires all three codes,
///   so there is no request shape that targets a General or Class account directly. The adapted,
///   real, testable rule is: a code triple that does not resolve to an actual Subsidiary row — even
///   one built from otherwise-real Class/General codes — fails path resolution exactly like an
///   unknown code, proving there is no "posting at a higher level" fallback.
/// - PATH-008/009/010 (archived Subsidiary/General/Class) all collapse to the same
///   <c>journal_entry_account_not_eligible</c> outcome, because <c>PostingEligible</c> is a single
///   boolean folding all three ancestor statuses. They are also not fully independent scenarios:
///   the Chart of Accounts spec requires a parent to have no active children before it can be
///   archived, so archiving a General or Class always requires its Subsidiary descendants to
///   already be archived too — there is no reachable state with an archived ancestor and an active
///   Subsidiary beneath it.
/// </summary>
public sealed class AccountPathScenarios(ApexWebApplicationFactory factory) : AccountingScenarioTestBase(factory)
{
    [Fact]
    [Trait("ScenarioId", "PATH-001")]
    public async Task ActiveCompletePath_ShouldAcceptPosting()
    {
        var scenario = await ArrangeArchivalChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);

        var posted = await PostAsync(scenario, date,
            Debit("ASSET", "01", "01", 500m, "Cash in"), Credit("EQUITY", "01", "01", 500m, "Capital in"));

        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(posted);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 500m);
    }

    [Fact]
    [Trait("ScenarioId", "PATH-002")]
    public async Task UnknownAccountClass_ShouldBeRejectedAtPosting()
    {
        var scenario = await ArrangeMultiPathChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        var draft = await scenario.CreateDraftEntryAsync(date, "Unknown class attempt",
            [Debit("ZZZZ", "01", "01", 100m, "Cash in"), Credit("EQUITY", "01", "01", 100m, "Capital in")]);
        Assert.True(draft.IsSuccess, draft.RawBody); // draft creation never checks path existence.

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.PostEntryAsync(draft.Value!.ReferenceNumber),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.InvalidAccountCodePath);
    }

    [Fact]
    [Trait("ScenarioId", "PATH-003")]
    public async Task UnknownGeneralAccount_ShouldBeRejectedAtPosting()
    {
        var scenario = await ArrangeMultiPathChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        var draft = await scenario.CreateDraftEntryAsync(date, "Unknown general attempt",
            [Debit("ASSET", "99", "01", 100m, "Cash in"), Credit("EQUITY", "01", "01", 100m, "Capital in")]);
        Assert.True(draft.IsSuccess, draft.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.PostEntryAsync(draft.Value!.ReferenceNumber),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.InvalidAccountCodePath);
    }

    [Fact]
    [Trait("ScenarioId", "PATH-004")]
    public async Task UnknownSubsidiaryAccount_ShouldBeRejectedAtPosting()
    {
        var scenario = await ArrangeMultiPathChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        var draft = await scenario.CreateDraftEntryAsync(date, "Unknown subsidiary attempt",
            [Debit("ASSET", "01", "99", 100m, "Cash in"), Credit("EQUITY", "01", "01", 100m, "Capital in")]);
        Assert.True(draft.IsSuccess, draft.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.PostEntryAsync(draft.Value!.ReferenceNumber),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.InvalidAccountCodePath);
    }

    [Fact]
    [Trait("ScenarioId", "PATH-005")]
    public async Task GeneralAccountFromAnotherAccountClass_ShouldBeRejectedAtPosting()
    {
        var scenario = await ArrangeMultiPathChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        // "09" is LIAB's General Account code, not one of ASSET's — the join requires
        // general_account.account_class_id to match ASSET's id, so this cannot resolve.
        var draft = await scenario.CreateDraftEntryAsync(date, "General from another class attempt",
            [Debit("ASSET", "09", "01", 100m, "Cash in"), Credit("EQUITY", "01", "01", 100m, "Capital in")]);
        Assert.True(draft.IsSuccess, draft.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.PostEntryAsync(draft.Value!.ReferenceNumber),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.InvalidAccountCodePath);
    }

    [Fact]
    [Trait("ScenarioId", "PATH-006")]
    public async Task SubsidiaryAccountFromAnotherGeneralAccount_ShouldBeRejectedAtPosting()
    {
        var scenario = await ArrangeMultiPathChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        // "05" is the Subsidiary Account under ASSET/02, not ASSET/01 — the join requires
        // subsidiary_account.general_account_id to match ASSET/01's id, so this cannot resolve.
        var draft = await scenario.CreateDraftEntryAsync(date, "Subsidiary from another general attempt",
            [Debit("ASSET", "01", "05", 100m, "Cash in"), Credit("EQUITY", "01", "01", 100m, "Capital in")]);
        Assert.True(draft.IsSuccess, draft.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.PostEntryAsync(draft.Value!.ReferenceNumber),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.InvalidAccountCodePath);
    }

    /// <summary>
    /// PATH-007 adapted (see class-level doc comment): there is no request shape that targets a
    /// General or Class account directly, since every line always supplies all three codes. This
    /// proves the underlying mechanism instead — a triple built from two genuinely existing
    /// ancestor codes (ASSET/02) but a Subsidiary code ("02") that does not exist under that
    /// General still fails exactly like any other unresolved path: there is no fallback that lets
    /// posting "land" on the General Account merely because its ancestors are real.
    /// </summary>
    [Fact]
    [Trait("ScenarioId", "PATH-007")]
    public async Task OnlyAGenuineSubsidiaryRow_ShouldBePostable()
    {
        var scenario = await ArrangeMultiPathChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        var draft = await scenario.CreateDraftEntryAsync(date, "No subsidiary-level fallback attempt",
            [Debit("ASSET", "02", "02", 100m, "Cash in"), Credit("EQUITY", "01", "01", 100m, "Capital in")]);
        Assert.True(draft.IsSuccess, draft.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.PostEntryAsync(draft.Value!.ReferenceNumber),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.InvalidAccountCodePath);
    }

    [Fact]
    [Trait("ScenarioId", "PATH-008")]
    public async Task ArchivedSubsidiaryAccount_ShouldRejectNewPosting()
    {
        var scenario = await ArrangeArchivalChartAsync();
        var subsidiaryId = scenario.Context.SubsidiaryAccountIdsByCode[("ASSET", "01", "01")];
        var archived = await Api.ArchiveSubsidiaryAccountAsync(subsidiaryId);
        Assert.True(archived.IsSuccess, archived.RawBody);
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        var draft = await scenario.CreateDraftEntryAsync(date, "Posting to archived subsidiary",
            [Debit("ASSET", "01", "01", 100m, "Cash in"), Credit("EQUITY", "01", "01", 100m, "Capital in")]);
        Assert.True(draft.IsSuccess, draft.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.PostEntryAsync(draft.Value!.ReferenceNumber),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.AccountNotEligible);
    }

    [Fact]
    [Trait("ScenarioId", "PATH-009")]
    public async Task ArchivedGeneralAccount_ShouldPreventPostingThroughDescendants()
    {
        var scenario = await ArrangeArchivalChartAsync();
        // A parent cannot be archived while it has active children — archive the Subsidiary first.
        var subsidiaryId = scenario.Context.SubsidiaryAccountIdsByCode[("ASSET", "01", "01")];
        Assert.True((await Api.ArchiveSubsidiaryAccountAsync(subsidiaryId)).IsSuccess);
        var generalId = scenario.Context.GeneralAccountIdsByCode[("ASSET", "01")];
        var archived = await Api.ArchiveGeneralAccountAsync(generalId);
        Assert.True(archived.IsSuccess, archived.RawBody);

        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        var draft = await scenario.CreateDraftEntryAsync(date, "Posting through archived general",
            [Debit("ASSET", "01", "01", 100m, "Cash in"), Credit("EQUITY", "01", "01", 100m, "Capital in")]);
        Assert.True(draft.IsSuccess, draft.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.PostEntryAsync(draft.Value!.ReferenceNumber),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.AccountNotEligible);
    }

    [Fact]
    [Trait("ScenarioId", "PATH-010")]
    public async Task ArchivedAccountClass_ShouldPreventPostingThroughDescendants()
    {
        var scenario = await ArrangeArchivalChartAsync();
        await ArchiveWholeAssetPathAsync(scenario);

        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        var draft = await scenario.CreateDraftEntryAsync(date, "Posting through archived class",
            [Debit("ASSET", "01", "01", 100m, "Cash in"), Credit("EQUITY", "01", "01", 100m, "Capital in")]);
        Assert.True(draft.IsSuccess, draft.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.PostEntryAsync(draft.Value!.ReferenceNumber),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.AccountNotEligible);
    }

    [Fact]
    [Trait("ScenarioId", "PATH-011")]
    public async Task ExistingHistory_ShouldRemainReportableAfterAccountArchival()
    {
        var scenario = await ArrangeArchivalChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        await PostAsync(scenario, date, Debit("ASSET", "01", "01", 750m, "Cash in"), Credit("EQUITY", "01", "01", 750m, "Capital in"));

        var subsidiaryId = scenario.Context.SubsidiaryAccountIdsByCode[("ASSET", "01", "01")];
        Assert.True((await Api.ArchiveSubsidiaryAccountAsync(subsidiaryId)).IsSuccess);

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 750m);
        var trialBalance = await Api.GetTrialBalanceAsync(scenario.Context.BookId, scenario.Context.FiscalYearId, date, date);
        Assert.True(trialBalance.IsSuccess, trialBalance.RawBody);
        Assert.Contains(trialBalance.Value!, item => item.AccountClassCode == "ASSET" && item.DebitTurnover == 750m);
    }

    [Fact]
    [Trait("ScenarioId", "PATH-012")]
    public async Task AccountRename_ShouldChangeDisplayNotStoredLines()
    {
        var scenario = await ArrangeArchivalChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        var posted = await PostAsync(scenario, date, Debit("ASSET", "01", "01", 300m, "Cash in"), Credit("EQUITY", "01", "01", 300m, "Capital in"));

        var subsidiaryId = scenario.Context.SubsidiaryAccountIdsByCode[("ASSET", "01", "01")];
        var renamed = await Api.UpdateSubsidiaryAccountAsync(subsidiaryId, "Renamed Cash Account");
        Assert.True(renamed.IsSuccess, renamed.RawBody);

        var account = await Api.GetAccountAsync("SubsidiaryAccount", subsidiaryId);
        Assert.True(account.IsSuccess, account.RawBody);
        Assert.Equal("Renamed Cash Account", account.Value!.Name);
        Assert.Equal("01", account.Value.Code);

        var lines = await Inspector.GetOrderedLinesAsync(posted.Id);
        Assert.All(lines, line => Assert.Equal("01", line.SubsidiaryAccountCode));
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 300m);
    }

    [Fact]
    [Trait("ScenarioId", "PATH-013")]
    public async Task AccountCodes_ShouldNotBeChangeable()
    {
        var scenario = await ArrangeArchivalChartAsync();
        var subsidiaryId = scenario.Context.SubsidiaryAccountIdsByCode[("ASSET", "01", "01")];
        var before = await Api.GetAccountAsync("SubsidiaryAccount", subsidiaryId);
        Assert.True(before.IsSuccess, before.RawBody);

        // UpdateSubsidiaryAccountRequest carries only a Name — there is no field to submit a new
        // code, so the code is structurally immutable through the public contract.
        var updated = await Api.UpdateSubsidiaryAccountAsync(subsidiaryId, "Any New Display Name");
        Assert.True(updated.IsSuccess, updated.RawBody);

        var after = await Api.GetAccountAsync("SubsidiaryAccount", subsidiaryId);
        Assert.True(after.IsSuccess, after.RawBody);
        Assert.Equal(before.Value!.Code, after.Value!.Code);
    }

    [Fact]
    [Trait("ScenarioId", "PATH-014")]
    public async Task AccountNatureAndParent_ShouldNotBeChangeable()
    {
        var scenario = await ArrangeArchivalChartAsync();
        var subsidiaryId = scenario.Context.SubsidiaryAccountIdsByCode[("ASSET", "01", "01")];
        var before = await Api.GetAccountAsync("SubsidiaryAccount", subsidiaryId);
        Assert.True(before.IsSuccess, before.RawBody);

        // Same structural proof as PATH-013: the update DTO exposes only Name, so Nature and
        // ParentId cannot be submitted at all through the public contract.
        var updated = await Api.UpdateSubsidiaryAccountAsync(subsidiaryId, "Renamed Without Changing Nature");
        Assert.True(updated.IsSuccess, updated.RawBody);

        var after = await Api.GetAccountAsync("SubsidiaryAccount", subsidiaryId);
        Assert.True(after.IsSuccess, after.RawBody);
        Assert.Equal(before.Value!.Nature, after.Value!.Nature);
        Assert.Equal(before.Value.ParentId, after.Value.ParentId);
    }

    [Fact]
    [Trait("ScenarioId", "PATH-015")]
    public async Task ReactivatedValidPath_ShouldAcceptPostingAgain()
    {
        var scenario = await ArrangeArchivalChartAsync();
        await ArchiveWholeAssetPathAsync(scenario);

        // Reactivating a child requires all its ancestors to already be active — reverse order.
        var classId = scenario.Context.AccountClassIdsByCode["ASSET"];
        var generalId = scenario.Context.GeneralAccountIdsByCode[("ASSET", "01")];
        var subsidiaryId = scenario.Context.SubsidiaryAccountIdsByCode[("ASSET", "01", "01")];
        Assert.True((await Api.ReactivateAccountClassAsync(classId)).IsSuccess);
        Assert.True((await Api.ReactivateGeneralAccountAsync(generalId)).IsSuccess);
        Assert.True((await Api.ReactivateSubsidiaryAccountAsync(subsidiaryId)).IsSuccess);

        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        var posted = await PostAsync(scenario, date,
            Debit("ASSET", "01", "01", 200m, "Cash in"), Credit("EQUITY", "01", "01", 200m, "Capital in"));

        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(posted);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 200m);
    }

    // ---------------------------------------------------------------------------------------
    // Arrangement helpers (setup only — the scenario's outcome is always asserted in the test body).
    // ---------------------------------------------------------------------------------------

    private async Task<AccountingScenario> ArrangeArchivalChartAsync()
    {
        var scenario = await NewScenarioAsync();
        await scenario.CreateBookAsync();
        await scenario.ActivateBookAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        await scenario.CreateAccountClassAsync("ASSET", "Assets");
        await scenario.CreateGeneralAccountAsync("ASSET", "01", "Cash and Banks", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync("ASSET", "01", "01", "Cash", AccountNature.Debtor, DetailAccountType.None);
        await scenario.CreateAccountClassAsync("EQUITY", "Equity");
        await scenario.CreateGeneralAccountAsync("EQUITY", "01", "Owner Capital", AccountNature.Creditor);
        await scenario.CreateSubsidiaryAccountAsync("EQUITY", "01", "01", "Capital", AccountNature.Creditor, DetailAccountType.None);
        return scenario;
    }

    /// <summary>Richer chart for PATH-002 through PATH-007: two Generals under ASSET (each with its
    /// own Subsidiary), plus a separate LIAB class, so "wrong parent" codes can be built from
    /// genuinely existing accounts rather than pure nonsense strings.</summary>
    private async Task<AccountingScenario> ArrangeMultiPathChartAsync()
    {
        var scenario = await NewScenarioAsync();
        await scenario.CreateBookAsync();
        await scenario.ActivateBookAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();

        await scenario.CreateAccountClassAsync("ASSET", "Assets");
        await scenario.CreateGeneralAccountAsync("ASSET", "01", "Cash and Banks", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync("ASSET", "01", "01", "Cash", AccountNature.Debtor, DetailAccountType.None);
        await scenario.CreateGeneralAccountAsync("ASSET", "02", "Other Assets", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync("ASSET", "02", "05", "Other Asset Holder", AccountNature.Debtor, DetailAccountType.None);

        await scenario.CreateAccountClassAsync("LIAB", "Liabilities");
        await scenario.CreateGeneralAccountAsync("LIAB", "09", "Payables", AccountNature.Creditor);
        await scenario.CreateSubsidiaryAccountAsync("LIAB", "09", "01", "Trade Payables", AccountNature.Creditor, DetailAccountType.None);

        await scenario.CreateAccountClassAsync("EQUITY", "Equity");
        await scenario.CreateGeneralAccountAsync("EQUITY", "01", "Owner Capital", AccountNature.Creditor);
        await scenario.CreateSubsidiaryAccountAsync("EQUITY", "01", "01", "Capital", AccountNature.Creditor, DetailAccountType.None);
        return scenario;
    }

    /// <summary>Archives the ASSET/01/01 path bottom-up (Subsidiary, then General, then Class) — the
    /// only reachable order, since a parent cannot be archived while it has active children.</summary>
    private async Task ArchiveWholeAssetPathAsync(AccountingScenario scenario)
    {
        var subsidiaryId = scenario.Context.SubsidiaryAccountIdsByCode[("ASSET", "01", "01")];
        var generalId = scenario.Context.GeneralAccountIdsByCode[("ASSET", "01")];
        var classId = scenario.Context.AccountClassIdsByCode["ASSET"];
        Assert.True((await Api.ArchiveSubsidiaryAccountAsync(subsidiaryId)).IsSuccess);
        Assert.True((await Api.ArchiveGeneralAccountAsync(generalId)).IsSuccess);
        Assert.True((await Api.ArchiveAccountClassAsync(classId)).IsSuccess);
    }

    /// <summary>Creates and posts a two-line balanced draft entry, failing fast if either step is rejected.</summary>
    private static async Task<JournalEntryDetailResponse> PostAsync(
        AccountingScenario scenario, DateOnly date, JournalEntryLineRequest debitLine, JournalEntryLineRequest creditLine)
    {
        var draft = await scenario.CreateDraftEntryAsync(date, "Path scenario entry", [debitLine, creditLine]);
        Assert.True(draft.IsSuccess, draft.RawBody);
        var posted = await scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);
        return posted.Value!;
    }
}
