using System.Net;
using Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.DetailAccounts.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using static Apex.IntegrationTests.Accounting.Scenarios.Infrastructure.AccountingScenario;
// Two different "DetailAccountType" enums are involved here: ChartOfAccounts.Domain.DetailAccountType
// is the *requirement* recorded on a Subsidiary Account (NONE/BANK/PERSON/SYMBOL); DetailAccounts.Domain
// DetailAccountType is the concrete Detail Account's own type (PERSON/SYMBOL/BANK, no NONE). Alias the
// former to keep every CreateSubsidiaryAccountAsync call unambiguous.
using SubsidiaryDetailRequirement = Apex.Modules.Accounting.ChartOfAccounts.Domain.DetailAccountType;

namespace Apex.IntegrationTests.Accounting.Scenarios;

/// <summary>
/// Covers the required scenario catalogue group E ("Detail Accounts", DETAIL-001 through
/// DETAIL-014 — spec §9.E).
///
/// Key mechanism, confirmed by reading <c>JournalEntryLineAssembler.BuildAsync</c> and
/// <c>ValidateDetailAccountForPostingHandler</c>: whenever a line's Class/General/Subsidiary path
/// already resolves (which it always does here, since every test builds real chart accounts first),
/// the Detail-Account requirement is validated at DRAFT CREATION time, not only at posting — so
/// most of these scenarios exercise <c>CreateDraftEntryAsync</c> directly rather than requiring a
/// Post. The validator's exact rules (all thrown from <c>DetailAccountErrors</c>, not the parallel
/// unused <c>JournalEntryErrors.DetailAccount*</c> constants):
/// <list type="bullet">
/// <item>Requirement <c>NONE</c> + a supplied code → 422 <c>detail_account_not_allowed</c>.</item>
/// <item>Requirement not <c>NONE</c> + no code → 422 <c>detail_account_required</c>.</item>
/// <item>Code supplied but unresolvable → 404 <c>detail_account_not_found</c> (a 404 from inside a
/// draft-creation POST — confirmed by reading the handler, not assumed).</item>
/// <item>Resolved but archived → 422 <c>detail_account_archived</c>.</item>
/// <item>Resolved, active, but wrong concrete type → 422 <c>detail_account_type_mismatch</c>.</item>
/// </list>
/// </summary>
public sealed class DetailAccountScenarios(ApexWebApplicationFactory factory) : AccountingScenarioTestBase(factory)
{
    [Fact]
    [Trait("ScenarioId", "DETAIL-001")]
    public async Task NoneRequirement_ShouldAcceptLineWithoutDetailAccount()
    {
        var scenario = await ArrangeDetailChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);

        var posted = await PostAsync(scenario, date, "ASSET", "01", "01", null, 100m);

        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(posted);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 100m);
    }

    [Fact]
    [Trait("ScenarioId", "DETAIL-002")]
    public async Task NoneRequirement_ShouldRejectSuppliedDetailAccount()
    {
        var scenario = await ArrangeDetailChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(date, "None requirement with a supplied detail account",
                [Debit("ASSET", "01", "01", 100m, "Cash in", "BANK-1"), Credit("EQUITY", "01", "01", 100m, "Capital in")]),
            HttpStatusCode.UnprocessableEntity, DetailAccountErrors.NotAllowed);
    }

    [Fact]
    [Trait("ScenarioId", "DETAIL-003")]
    public async Task BankRequirement_ShouldAcceptAnActiveBankDetailAccount()
    {
        var scenario = await ArrangeDetailChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);

        var posted = await PostAsync(scenario, date, "ASSET", "02", "01", "BANK-1", 250m);

        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(posted);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "02", "01", null, date, 250m);
    }

    [Fact]
    [Trait("ScenarioId", "DETAIL-004")]
    public async Task BankRequirement_ShouldRejectPersonAndSymbolDetailAccounts()
    {
        var scenario = await ArrangeDetailChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(date, "Bank requirement with a Person detail account",
                [Debit("ASSET", "02", "01", 100m, "Cash in", "PERSON-1"), Credit("EQUITY", "01", "01", 100m, "Capital in")]),
            HttpStatusCode.UnprocessableEntity, DetailAccountErrors.TypeMismatch);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(date, "Bank requirement with a Symbol detail account",
                [Debit("ASSET", "02", "01", 100m, "Cash in", "SYMBOL-1"), Credit("EQUITY", "01", "01", 100m, "Capital in")]),
            HttpStatusCode.UnprocessableEntity, DetailAccountErrors.TypeMismatch);
    }

    [Fact]
    [Trait("ScenarioId", "DETAIL-005")]
    public async Task PersonRequirement_ShouldAcceptPersonAndRejectBankAndSymbol()
    {
        var scenario = await ArrangeDetailChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);

        var posted = await PostAsync(scenario, date, "ASSET", "03", "01", "PERSON-1", 80m);
        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(posted);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(date, "Person requirement with a Bank detail account",
                [Debit("ASSET", "03", "01", 100m, "Cash in", "BANK-1"), Credit("EQUITY", "01", "01", 100m, "Capital in")]),
            HttpStatusCode.UnprocessableEntity, DetailAccountErrors.TypeMismatch);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(date, "Person requirement with a Symbol detail account",
                [Debit("ASSET", "03", "01", 100m, "Cash in", "SYMBOL-1"), Credit("EQUITY", "01", "01", 100m, "Capital in")]),
            HttpStatusCode.UnprocessableEntity, DetailAccountErrors.TypeMismatch);
    }

    [Fact]
    [Trait("ScenarioId", "DETAIL-006")]
    public async Task SymbolRequirement_ShouldAcceptSymbolAndRejectBankAndPerson()
    {
        var scenario = await ArrangeDetailChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);

        var posted = await PostAsync(scenario, date, "ASSET", "04", "01", "SYMBOL-1", 60m);
        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(posted);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(date, "Symbol requirement with a Bank detail account",
                [Debit("ASSET", "04", "01", 100m, "Cash in", "BANK-1"), Credit("EQUITY", "01", "01", 100m, "Capital in")]),
            HttpStatusCode.UnprocessableEntity, DetailAccountErrors.TypeMismatch);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(date, "Symbol requirement with a Person detail account",
                [Debit("ASSET", "04", "01", 100m, "Cash in", "PERSON-1"), Credit("EQUITY", "01", "01", 100m, "Capital in")]),
            HttpStatusCode.UnprocessableEntity, DetailAccountErrors.TypeMismatch);
    }

    [Fact]
    [Trait("ScenarioId", "DETAIL-007")]
    public async Task MissingRequiredDetailAccount_ShouldBeRejected()
    {
        var scenario = await ArrangeDetailChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(date, "Missing required detail account",
                [Debit("ASSET", "02", "01", 100m, "Cash in"), Credit("EQUITY", "01", "01", 100m, "Capital in")]),
            HttpStatusCode.UnprocessableEntity, DetailAccountErrors.Required);
    }

    [Fact]
    [Trait("ScenarioId", "DETAIL-008")]
    public async Task UnknownDetailAccountCode_ShouldBeRejected()
    {
        var scenario = await ArrangeDetailChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(date, "Unknown detail account code",
                [Debit("ASSET", "02", "01", 100m, "Cash in", "NOPE-999"), Credit("EQUITY", "01", "01", 100m, "Capital in")]),
            HttpStatusCode.NotFound, DetailAccountErrors.NotFound);
    }

    [Fact]
    [Trait("ScenarioId", "DETAIL-009")]
    public async Task ArchivedDetailAccount_ShouldBeRejectedForNewPosting()
    {
        var scenario = await ArrangeDetailChartAsync();
        var archived = await Api.ArchiveDetailAccountAsync(scenario.BankOneId);
        Assert.True(archived.IsSuccess, archived.RawBody);
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(date, "Posting with an archived detail account",
                [Debit("ASSET", "02", "01", 100m, "Cash in", "BANK-1"), Credit("EQUITY", "01", "01", 100m, "Capital in")]),
            HttpStatusCode.UnprocessableEntity, DetailAccountErrors.Archived);
    }

    [Fact]
    [Trait("ScenarioId", "DETAIL-010")]
    public async Task HistoricalBalances_ShouldRemainAvailableAfterDetailAccountArchival()
    {
        var scenario = await ArrangeDetailChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        await PostAsync(scenario, date, "ASSET", "02", "01", "BANK-1", 400m);

        var archived = await Api.ArchiveDetailAccountAsync(scenario.BankOneId);
        Assert.True(archived.IsSuccess, archived.RawBody);

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "02", "01", "BANK-1", date, 400m);
    }

    [Fact]
    [Trait("ScenarioId", "DETAIL-011")]
    public async Task RenamingDetailAccount_ShouldChangeDisplayNotStoredLinesOrBalances()
    {
        var scenario = await ArrangeDetailChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        var posted = await PostAsync(scenario, date, "ASSET", "02", "01", "BANK-1", 350m);

        var renamed = await Api.UpdateDetailAccountAsync(scenario.BankOneId, "Renamed Bank Detail", ScenarioDefaults.DetailAccountTypeBank);
        Assert.True(renamed.IsSuccess, renamed.RawBody);

        var fetched = await Api.GetDetailAccountAsync(scenario.BankOneId);
        Assert.True(fetched.IsSuccess, fetched.RawBody);
        Assert.Equal("Renamed Bank Detail", fetched.Value!.Name);

        var lines = await Inspector.GetOrderedLinesAsync(posted.Id);
        Assert.Equal("BANK-1", Assert.Single(lines, line => line.Side == "DEBIT").DetailAccountCode);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "02", "01", "BANK-1", date, 350m);
    }

    [Fact]
    [Trait("ScenarioId", "DETAIL-012")]
    public async Task ChangingDetailAccountType_ShouldAffectFutureEligibilityNotHistory()
    {
        var scenario = await ArrangeDetailChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        await PostAsync(scenario, date, "ASSET", "02", "01", "BANK-1", 500m);

        var retyped = await Api.UpdateDetailAccountAsync(scenario.BankOneId, "Bank Detail A", ScenarioDefaults.DetailAccountTypePerson);
        Assert.True(retyped.IsSuccess, retyped.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(date.AddDays(1), "Posting after detail account retype",
                [Debit("ASSET", "02", "01", 50m, "Cash in", "BANK-1"), Credit("EQUITY", "01", "01", 50m, "Capital in")]),
            HttpStatusCode.UnprocessableEntity, DetailAccountErrors.TypeMismatch);

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "02", "01", "BANK-1", date, 500m);
    }

    [Fact]
    [Trait("ScenarioId", "DETAIL-013")]
    public async Task TwoDetailCodesUnderOneSubsidiaryAccount_ShouldRemainSeparatelyReportable()
    {
        var scenario = await ArrangeDetailChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(3);
        await PostAsync(scenario, date, "ASSET", "02", "01", "BANK-1", 60m);
        await PostAsync(scenario, date, "ASSET", "02", "01", "BANK-2", 40m);

        var bankOneTurnover = await Inspector.GetAggregateTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, date, "ASSET", "02", "01", "BANK-1");
        Assert.Equal(60m, bankOneTurnover.Debit);
        var bankTwoTurnover = await Inspector.GetAggregateTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, date, "ASSET", "02", "01", "BANK-2");
        Assert.Equal(40m, bankTwoTurnover.Debit);
    }

    [Fact]
    [Trait("ScenarioId", "DETAIL-014")]
    public async Task DetailAccountCode_ShouldBeImmutable()
    {
        var scenario = await ArrangeDetailChartAsync();

        var result = await Api.UpdateDetailAccountAsync(
            scenario.BankOneId, "Bank Detail A", ScenarioDefaults.DetailAccountTypeBank, code: "DIFFERENT-CODE");

        ScenarioAssertions.AssertRejected(result, HttpStatusCode.UnprocessableEntity, DetailAccountErrors.CodeImmutable);
    }

    // ---------------------------------------------------------------------------------------
    // Arrangement helpers (setup only — the scenario's outcome is always asserted in the test body).
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// One Subsidiary Account per Detail-Account requirement (ASSET/01=NONE, ASSET/02=BANK,
    /// ASSET/03=PERSON, ASSET/04=SYMBOL), a single EQUITY/01/01 credit counterpart, and four concrete
    /// Detail Accounts (two Bank, one Person, one Symbol) covering every requirement/type
    /// combination the catalogue needs.
    /// </summary>
    private async Task<DetailScenario> ArrangeDetailChartAsync()
    {
        var scenario = await NewScenarioAsync();
        await scenario.CreateBookAsync();
        await scenario.ActivateBookAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();

        await scenario.CreateAccountClassAsync("ASSET", "Assets");
        await scenario.CreateGeneralAccountAsync("ASSET", "01", "Cash", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync("ASSET", "01", "01", "Cash", AccountNature.Debtor, SubsidiaryDetailRequirement.None);
        await scenario.CreateGeneralAccountAsync("ASSET", "02", "Bank Accounts", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync("ASSET", "02", "01", "Bank Holder", AccountNature.Debtor, SubsidiaryDetailRequirement.Bank);
        await scenario.CreateGeneralAccountAsync("ASSET", "03", "Receivables", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync("ASSET", "03", "01", "Person Holder", AccountNature.Debtor, SubsidiaryDetailRequirement.Person);
        await scenario.CreateGeneralAccountAsync("ASSET", "04", "Symbol Holdings", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync("ASSET", "04", "01", "Symbol Holder", AccountNature.Debtor, SubsidiaryDetailRequirement.Symbol);

        await scenario.CreateAccountClassAsync("EQUITY", "Equity");
        await scenario.CreateGeneralAccountAsync("EQUITY", "01", "Owner Capital", AccountNature.Creditor);
        await scenario.CreateSubsidiaryAccountAsync("EQUITY", "01", "01", "Capital", AccountNature.Creditor, SubsidiaryDetailRequirement.None);

        var bankOne = await Api.CreateDetailAccountAsync("BANK-1", "Bank Detail A", ScenarioDefaults.DetailAccountTypeBank);
        Assert.True(bankOne.IsSuccess, bankOne.RawBody);
        var bankTwo = await Api.CreateDetailAccountAsync("BANK-2", "Bank Detail B", ScenarioDefaults.DetailAccountTypeBank);
        Assert.True(bankTwo.IsSuccess, bankTwo.RawBody);
        var personOne = await Api.CreateDetailAccountAsync("PERSON-1", "Person Detail A", ScenarioDefaults.DetailAccountTypePerson);
        Assert.True(personOne.IsSuccess, personOne.RawBody);
        var symbolOne = await Api.CreateDetailAccountAsync("SYMBOL-1", "Symbol Detail A", ScenarioDefaults.DetailAccountTypeSymbol);
        Assert.True(symbolOne.IsSuccess, symbolOne.RawBody);

        return new DetailScenario(scenario, bankOne.Value!.Id, bankTwo.Value!.Id, personOne.Value!.Id, symbolOne.Value!.Id);
    }

    /// <summary>Creates and posts a two-line balanced draft entry (debit leg carries the detail
    /// account under test), failing fast if either step is rejected.</summary>
    private static async Task<JournalEntryDetailResponse> PostAsync(
        DetailScenario scenario, DateOnly date, string classCode, string generalCode, string subsidiaryCode,
        string? detailCode, decimal amount)
    {
        var draft = await scenario.Scenario.CreateDraftEntryAsync(date, "Detail account scenario entry",
            [Debit(classCode, generalCode, subsidiaryCode, amount, "Cash in", detailCode),
             Credit("EQUITY", "01", "01", amount, "Capital in")]);
        Assert.True(draft.IsSuccess, draft.RawBody);
        var posted = await scenario.Scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);
        return posted.Value!;
    }

    /// <summary>Wraps the base <see cref="AccountingScenario"/> with the ids of the four concrete
    /// Detail Accounts created for this test class, plus implicit forwarding of Context for
    /// convenience at call sites.</summary>
    private sealed record DetailScenario(AccountingScenario Scenario, long BankOneId, long BankTwoId, long PersonOneId, long SymbolOneId)
    {
        public AccountingScenarioContext Context => Scenario.Context;

        public Task<ScenarioApiResult<JournalEntryDetailResponse>> CreateDraftEntryAsync(
            DateOnly date, string description, IReadOnlyList<JournalEntryLineRequest> lines) =>
            Scenario.CreateDraftEntryAsync(date, description, lines);

        public Task<ScenarioApiResult<JournalEntryDetailResponse>> PostEntryAsync(long referenceNumber) =>
            Scenario.PostEntryAsync(referenceNumber);
    }
}
