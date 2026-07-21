using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using Apex.Modules.Accounting.JournalEntries.UseCases.CreateDraftJournalEntry;

namespace Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;

/// <summary>
/// Fluent Accounting-specific scenario driver (spec §6). Setup steps assert success internally
/// (fail fast with the response body in the exception message) and return
/// <see cref="Task{AccountingScenario}"/> for chaining; the business-outcome operations
/// (<see cref="CreateDraftEntryAsync"/>, <see cref="PostEntryAsync"/>,
/// <see cref="ReverseEntryAsync"/>) return the typed <see cref="ScenarioApiResult{T}"/> directly
/// so the test asserts the outcome itself. No reflection, dynamic dispatch, or generic command
/// bus (spec §6.1) — every operation is its own named method.
/// </summary>
public sealed class AccountingScenario
{
    private readonly ScenarioApiClient _api;

    private AccountingScenario(ScenarioApiClient api, AccountingScenarioContext context)
    {
        _api = api;
        Context = context;
    }

    public AccountingScenarioContext Context { get; }

    public static AccountingScenario Start(ScenarioApiClient api) => new(api, new AccountingScenarioContext());

    public async Task<AccountingScenario> CreateBookAsync(
        string? code = null, string? title = null, CancellationToken cancellationToken = default)
    {
        var bookCode = code ?? ScenarioDefaults.UniqueCode("BOOK");
        var result = await _api.CreateBookAsync(bookCode, title ?? bookCode, cancellationToken: cancellationToken);
        AssertSetupSuccess(result, $"create accounting book '{bookCode}'");
        Context.BookId = result.Value!.Id;
        Context.BookCode = result.Value.Code;
        return this;
    }

    public async Task<AccountingScenario> ActivateBookAsync(CancellationToken cancellationToken = default)
    {
        var result = await _api.ActivateBookAsync(Context.BookId, cancellationToken);
        AssertSetupSuccess(result, $"activate accounting book {Context.BookId}");
        return this;
    }

    public async Task<AccountingScenario> CreateAccountClassAsync(
        string code, string name, CancellationToken cancellationToken = default)
    {
        var result = await _api.CreateAccountClassAsync(code, name, cancellationToken);
        AssertSetupSuccess(result, $"create account class '{code}'");
        Context.RecordAccountClass(result.Value!.Code, result.Value.Id);
        return this;
    }

    public async Task<AccountingScenario> CreateGeneralAccountAsync(
        string accountClassCode, string code, string name, AccountNature nature,
        CancellationToken cancellationToken = default)
    {
        var accountClassId = RequireAccountClassId(accountClassCode);
        var result = await _api.CreateGeneralAccountAsync(accountClassId, code, name, nature, cancellationToken);
        AssertSetupSuccess(result, $"create general account '{code}' under class '{accountClassCode}'");
        Context.RecordGeneralAccount(accountClassCode, result.Value!.Code, result.Value.Id);
        return this;
    }

    public async Task<AccountingScenario> CreateSubsidiaryAccountAsync(
        string accountClassCode, string generalAccountCode, string code, string name, AccountNature nature,
        DetailAccountType detailAccountType, CancellationToken cancellationToken = default)
    {
        var generalAccountId = RequireGeneralAccountId(accountClassCode, generalAccountCode);
        var result = await _api.CreateSubsidiaryAccountAsync(
            generalAccountId, code, name, nature, detailAccountType, cancellationToken);
        AssertSetupSuccess(
            result, $"create subsidiary account '{code}' under general account '{accountClassCode}/{generalAccountCode}'");
        Context.RecordSubsidiaryAccount(accountClassCode, generalAccountCode, result.Value!.Code, result.Value.Id);
        return this;
    }

    public async Task<AccountingScenario> CreateDetailAccountAsync(
        string code, string name, string type, CancellationToken cancellationToken = default)
    {
        var result = await _api.CreateDetailAccountAsync(code, name, type, cancellationToken);
        AssertSetupSuccess(result, $"create detail account '{code}'");
        Context.RecordDetailAccount(result.Value!.Code);
        return this;
    }

    public async Task<AccountingScenario> CreateFiscalYearAsync(
        string title, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var result = await _api.CreateFiscalYearAsync(Context.BookId, title, startDate, endDate, cancellationToken);
        AssertSetupSuccess(result, $"create fiscal year '{title}'");
        Context.FiscalYearId = result.Value!.Id;
        Context.FiscalYearTitle = result.Value.Title;
        Context.FiscalYearStartDate = result.Value.StartDate;
        Context.FiscalYearEndDate = result.Value.EndDate;
        return this;
    }

    public async Task<AccountingScenario> OpenFiscalYearAsync(CancellationToken cancellationToken = default)
    {
        var result = await _api.OpenFiscalYearAsync(Context.FiscalYearId, cancellationToken);
        AssertSetupSuccess(result, $"open fiscal year {Context.FiscalYearId}");
        return this;
    }

    /// <summary>Creates a draft Journal Entry. The result is returned directly — most tests assert
    /// on the draft itself (status, balances untouched, etc.) rather than treating it as setup.</summary>
    public async Task<ScenarioApiResult<JournalEntryDetailResponse>> CreateDraftEntryAsync(
        DateOnly date,
        string description,
        IReadOnlyList<JournalEntryLineRequest> lines,
        string documentType = ScenarioDefaults.DocumentTypeGeneral,
        string insertionType = ScenarioDefaults.InsertionTypeManual,
        string balanceEffect = ScenarioDefaults.BalanceEffectFinancial,
        string? sourceType = null,
        string? sourceReference = null,
        long? accountingBookId = null,
        long? fiscalYearId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateDraftJournalEntryRequest
        {
            AccountingBookId = accountingBookId ?? Context.BookId,
            FiscalYearId = fiscalYearId ?? Context.FiscalYearId,
            AccountingDate = date,
            Description = description,
            DocumentType = documentType,
            InsertionType = insertionType,
            BalanceEffect = balanceEffect,
            SourceType = sourceType,
            SourceReference = sourceReference,
            Lines = lines
        };
        var result = await _api.CreateDraftEntryAsync(request, cancellationToken);
        if (result.IsSuccess)
            Context.RecordEntry(result.Value!);
        return result;
    }

    public async Task<ScenarioApiResult<JournalEntryDetailResponse>> PostEntryAsync(
        long referenceNumber, CancellationToken cancellationToken = default)
    {
        var entry = RequireEntry(referenceNumber);
        var result = await _api.PostEntryAsync(Context.FiscalYearId, entry.Id, cancellationToken);
        if (result.IsSuccess)
            Context.RecordEntry(result.Value!);
        return result;
    }

    public async Task<ScenarioApiResult<JournalEntryDetailResponse>> ReverseEntryAsync(
        long originalReferenceNumber, DateOnly accountingDate, string reversalReason,
        CancellationToken cancellationToken = default)
    {
        var result = await _api.ReverseEntryAsync(
            Context.FiscalYearId, originalReferenceNumber, accountingDate, reversalReason, cancellationToken);
        if (result.IsSuccess)
            Context.RecordEntry(result.Value!);
        return result;
    }

    /// <summary>Looks up a Journal Entry created earlier in this scenario by its Reference Number.</summary>
    public JournalEntryDetailResponse RequireEntry(long referenceNumber) =>
        Context.EntriesByReferenceNumber.TryGetValue(referenceNumber, out var entry)
            ? entry
            : throw new InvalidOperationException(
                $"No journal entry with Reference Number {referenceNumber} was recorded in this scenario.");

    public static JournalEntryLineRequest Debit(
        string accountClassCode, string generalAccountCode, string subsidiaryAccountCode, decimal amount,
        string description, string? detailAccountCode = null, int? rowNumber = null) =>
        Line(ScenarioDefaults.SideDebit, accountClassCode, generalAccountCode, subsidiaryAccountCode, amount,
            description, detailAccountCode, rowNumber);

    public static JournalEntryLineRequest Credit(
        string accountClassCode, string generalAccountCode, string subsidiaryAccountCode, decimal amount,
        string description, string? detailAccountCode = null, int? rowNumber = null) =>
        Line(ScenarioDefaults.SideCredit, accountClassCode, generalAccountCode, subsidiaryAccountCode, amount,
            description, detailAccountCode, rowNumber);

    private static JournalEntryLineRequest Line(
        string side, string accountClassCode, string generalAccountCode, string subsidiaryAccountCode,
        decimal amount, string description, string? detailAccountCode, int? rowNumber) => new()
        {
            Side = side,
            Amount = amount,
            AccountClassCode = accountClassCode,
            GeneralAccountCode = generalAccountCode,
            SubsidiaryAccountCode = subsidiaryAccountCode,
            DetailAccountCode = detailAccountCode,
            Description = description,
            RowNumber = rowNumber
        };

    private long RequireAccountClassId(string accountClassCode) =>
        Context.AccountClassIdsByCode.TryGetValue(accountClassCode, out var id)
            ? id
            : throw new InvalidOperationException(
                $"Account class '{accountClassCode}' was not created in this scenario yet.");

    private long RequireGeneralAccountId(string accountClassCode, string generalAccountCode) =>
        Context.GeneralAccountIdsByCode.TryGetValue((accountClassCode, generalAccountCode), out var id)
            ? id
            : throw new InvalidOperationException(
                $"General account '{accountClassCode}/{generalAccountCode}' was not created in this scenario yet.");

    private static void AssertSetupSuccess<T>(ScenarioApiResult<T> result, string operation)
    {
        if (!result.IsSuccess)
            throw new InvalidOperationException(
                $"Scenario setup failed while trying to {operation}: HTTP {(int)result.StatusCode} " +
                $"{result.StatusCode} — {result.RawBody}");
    }
}
