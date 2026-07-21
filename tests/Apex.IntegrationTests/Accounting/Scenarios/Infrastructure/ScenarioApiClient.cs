using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ActivateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ArchiveAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.GetAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.SuspendAccountingBook;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateAccountClass;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateGeneralAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateSubsidiaryAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.GetAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.GetAccountTree;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.SearchAccounts;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateAccountClass;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateGeneralAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateSubsidiaryAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.CreateDetailAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.GetDetailAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.GetDetailAccountByCode;
using Apex.Modules.Accounting.DetailAccounts.UseCases.ListDetailAccounts;
using Apex.Modules.Accounting.DetailAccounts.UseCases.SearchDetailAccountsForPosting;
using Apex.Modules.Accounting.DetailAccounts.UseCases.UpdateDetailAccount;
using Apex.Modules.Accounting.FiscalYears.UseCases.CancelFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.CreateFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.GetFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.OpenFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.ResolveFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.UpdateFiscalYear;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using Apex.Modules.Accounting.JournalEntries.UseCases.AppendDraftLines;
using Apex.Modules.Accounting.JournalEntries.UseCases.CreateDraftJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.GetAccountBalances;
using Apex.Modules.Accounting.JournalEntries.UseCases.GetCrossFiscalYearTurnover;
using Apex.Modules.Accounting.JournalEntries.UseCases.GetJournalEntryAudit;
using Apex.Modules.Accounting.JournalEntries.UseCases.GetTransactionReport;
using Apex.Modules.Accounting.JournalEntries.UseCases.GetTrialBalance;
using Apex.Modules.Accounting.JournalEntries.UseCases.RebuildJournalEntryProjections;
using Apex.Modules.Accounting.JournalEntries.UseCases.ReconcileJournalEntryProjections;
using Apex.Modules.Accounting.JournalEntries.UseCases.ReplaceDraftLines;
using Apex.Modules.Accounting.JournalEntries.UseCases.Reporting;
using Apex.Modules.Accounting.JournalEntries.UseCases.ReverseJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.SearchJournalEntries;
using Apex.Modules.Accounting.JournalEntries.UseCases.UpdateDraftJournalEntry;

namespace Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;

/// <summary>
/// Thin, typed HTTP wrapper around the Accounting public API. One named method per real
/// operation — no generic command dispatch (spec §6.1). Centralizes JSON options and Problem
/// Details parsing so scenario tests and the <see cref="AccountingScenario"/> driver never touch
/// raw <see cref="JsonDocument"/> values.
/// </summary>
public sealed class ScenarioApiClient
{
    private const string BooksBaseUrl = "/api/v1/accounting/books";
    private const string FiscalYearsBaseUrl = "/api/v1/accounting/fiscal-years";
    private const string ChartOfAccountsBaseUrl = "/api/v1/accounting/chart-of-accounts";
    private const string DetailAccountsBaseUrl = "/api/v1/accounting/detail-accounts";
    private const string JournalEntriesBaseUrl = "/api/v1/accounting/journal-entries";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
    };

    private readonly HttpClient _client;
    private readonly bool _unauthenticated;

    public ScenarioApiClient(HttpClient client) : this(client, unauthenticated: false) { }

    private ScenarioApiClient(HttpClient client, bool unauthenticated)
    {
        _client = client;
        _unauthenticated = unauthenticated;
    }

    /// <summary>Returns a client variant that marks every request as unauthenticated (401 path).</summary>
    public ScenarioApiClient AsUnauthenticated() => new(_client, unauthenticated: true);

    // ---------------------------------------------------------------------------------------
    // Accounting Books
    // ---------------------------------------------------------------------------------------

    public Task<ScenarioApiResult<CreateAccountingBookResponse>> CreateBookAsync(
        string code, string title, string ownerType = "SCENARIO", string? ownerId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<CreateAccountingBookResponse>(HttpMethod.Post, BooksBaseUrl,
            new CreateAccountingBookRequest
            {
                Code = code,
                Title = title,
                OwnerType = ownerType,
                OwnerId = ownerId ?? ScenarioDefaults.UniqueCode("owner")
            },
            cancellationToken);

    public Task<ScenarioApiResult<ActivateAccountingBookResponse>> ActivateBookAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<ActivateAccountingBookResponse>(HttpMethod.Post, $"{BooksBaseUrl}/{id}/activate", null, cancellationToken);

    public Task<ScenarioApiResult<SuspendAccountingBookResponse>> SuspendBookAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<SuspendAccountingBookResponse>(HttpMethod.Post, $"{BooksBaseUrl}/{id}/suspend", null, cancellationToken);

    public Task<ScenarioApiResult<ArchiveAccountingBookResponse>> ArchiveBookAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<ArchiveAccountingBookResponse>(HttpMethod.Post, $"{BooksBaseUrl}/{id}/archive", null, cancellationToken);

    public Task<ScenarioApiResult<GetAccountingBookResponse>> GetBookAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<GetAccountingBookResponse>(HttpMethod.Get, $"{BooksBaseUrl}/{id}", null, cancellationToken);

    // ---------------------------------------------------------------------------------------
    // Fiscal Years
    // ---------------------------------------------------------------------------------------

    public Task<ScenarioApiResult<CreateFiscalYearResponse>> CreateFiscalYearAsync(
        long accountingBookId, string title, DateOnly startDate, DateOnly endDate,
        CancellationToken cancellationToken = default) =>
        SendAsync<CreateFiscalYearResponse>(HttpMethod.Post, FiscalYearsBaseUrl,
            new CreateFiscalYearRequest
            {
                AccountingBookId = accountingBookId, Title = title, StartDate = startDate, EndDate = endDate
            },
            cancellationToken);

    public Task<ScenarioApiResult<OpenFiscalYearResponse>> OpenFiscalYearAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<OpenFiscalYearResponse>(HttpMethod.Post, $"{FiscalYearsBaseUrl}/{id}/open", null, cancellationToken);

    public Task<ScenarioApiResult<UpdateFiscalYearResponse>> UpdateFiscalYearAsync(
        long id, string title, DateOnly startDate, DateOnly endDate,
        CancellationToken cancellationToken = default) =>
        SendAsync<UpdateFiscalYearResponse>(HttpMethod.Put, $"{FiscalYearsBaseUrl}/{id}",
            new UpdateFiscalYearRequest { Title = title, StartDate = startDate, EndDate = endDate },
            cancellationToken);

    public Task<ScenarioApiResult<object?>> DeleteFiscalYearAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<object?>(HttpMethod.Delete, $"{FiscalYearsBaseUrl}/{id}", null, cancellationToken);

    public Task<ScenarioApiResult<FinalizeFiscalYearResponse>> FinalizeFiscalYearAsync(
        long id, DateOnly finalizedThroughDate, CancellationToken cancellationToken = default) =>
        SendAsync<FinalizeFiscalYearResponse>(HttpMethod.Post, $"{FiscalYearsBaseUrl}/{id}/finalize",
            new FinalizeFiscalYearRequest { FinalizedThroughDate = finalizedThroughDate }, cancellationToken);

    public Task<ScenarioApiResult<CancelFiscalYearResponse>> CancelFiscalYearAsync(
        long id, DateOnly cancellationDate, CancellationToken cancellationToken = default) =>
        SendAsync<CancelFiscalYearResponse>(HttpMethod.Post, $"{FiscalYearsBaseUrl}/{id}/cancel",
            new CancelFiscalYearRequest { CancellationDate = cancellationDate }, cancellationToken);

    public Task<ScenarioApiResult<ResolveFiscalYearResponse>> ResolveFiscalYearAsync(
        long accountingBookId, DateOnly accountingDate, string? requiredStatus = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("accountingBookId", accountingBookId),
            ("accountingDate", accountingDate),
            ("requiredStatus", requiredStatus));
        return SendAsync<ResolveFiscalYearResponse>(HttpMethod.Get, $"{FiscalYearsBaseUrl}/resolve{query}", null, cancellationToken);
    }

    public Task<ScenarioApiResult<object?>> RepairFiscalYearDirectoryAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<object?>(HttpMethod.Post, $"{FiscalYearsBaseUrl}/{id}/repair-directory-index", null, cancellationToken);

    public Task<ScenarioApiResult<GetFiscalYearResponse>> GetFiscalYearAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<GetFiscalYearResponse>(HttpMethod.Get, $"{FiscalYearsBaseUrl}/{id}", null, cancellationToken);

    // ---------------------------------------------------------------------------------------
    // Chart of Accounts
    // ---------------------------------------------------------------------------------------

    internal Task<ScenarioApiResult<CreateAccountClassResponse>> CreateAccountClassAsync(
        string code, string name, CancellationToken cancellationToken = default) =>
        SendAsync<CreateAccountClassResponse>(HttpMethod.Post, $"{ChartOfAccountsBaseUrl}/classes",
            new CreateAccountClassRequest(code, name), cancellationToken);

    internal Task<ScenarioApiResult<UpdateAccountClassResponse>> UpdateAccountClassAsync(
        long id, string name, CancellationToken cancellationToken = default) =>
        SendAsync<UpdateAccountClassResponse>(HttpMethod.Put, $"{ChartOfAccountsBaseUrl}/classes/{id}",
            new UpdateAccountClassRequest(name), cancellationToken);

    public Task<ScenarioApiResult<object?>> ArchiveAccountClassAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<object?>(HttpMethod.Post, $"{ChartOfAccountsBaseUrl}/classes/{id}/archive", null, cancellationToken);

    public Task<ScenarioApiResult<object?>> ReactivateAccountClassAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<object?>(HttpMethod.Post, $"{ChartOfAccountsBaseUrl}/classes/{id}/reactivate", null, cancellationToken);

    internal Task<ScenarioApiResult<CreateGeneralAccountResponse>> CreateGeneralAccountAsync(
        long accountClassId, string code, string name, AccountNature nature,
        CancellationToken cancellationToken = default) =>
        SendAsync<CreateGeneralAccountResponse>(HttpMethod.Post, $"{ChartOfAccountsBaseUrl}/general-accounts",
            new CreateGeneralAccountRequest(accountClassId, code, name, nature), cancellationToken);

    internal Task<ScenarioApiResult<UpdateGeneralAccountResponse>> UpdateGeneralAccountAsync(
        long id, string name, CancellationToken cancellationToken = default) =>
        SendAsync<UpdateGeneralAccountResponse>(HttpMethod.Put, $"{ChartOfAccountsBaseUrl}/general-accounts/{id}",
            new UpdateGeneralAccountRequest(name), cancellationToken);

    public Task<ScenarioApiResult<object?>> ArchiveGeneralAccountAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<object?>(HttpMethod.Post, $"{ChartOfAccountsBaseUrl}/general-accounts/{id}/archive", null, cancellationToken);

    public Task<ScenarioApiResult<object?>> ReactivateGeneralAccountAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<object?>(HttpMethod.Post, $"{ChartOfAccountsBaseUrl}/general-accounts/{id}/reactivate", null, cancellationToken);

    internal Task<ScenarioApiResult<CreateSubsidiaryAccountResponse>> CreateSubsidiaryAccountAsync(
        long generalAccountId, string code, string name, AccountNature nature, DetailAccountType detailAccountType,
        CancellationToken cancellationToken = default) =>
        SendAsync<CreateSubsidiaryAccountResponse>(HttpMethod.Post, $"{ChartOfAccountsBaseUrl}/subsidiary-accounts",
            new CreateSubsidiaryAccountRequest(generalAccountId, code, name, nature, detailAccountType),
            cancellationToken);

    internal Task<ScenarioApiResult<UpdateSubsidiaryAccountResponse>> UpdateSubsidiaryAccountAsync(
        long id, string name, CancellationToken cancellationToken = default) =>
        SendAsync<UpdateSubsidiaryAccountResponse>(HttpMethod.Put, $"{ChartOfAccountsBaseUrl}/subsidiary-accounts/{id}",
            new UpdateSubsidiaryAccountRequest(name), cancellationToken);

    public Task<ScenarioApiResult<object?>> ArchiveSubsidiaryAccountAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<object?>(HttpMethod.Post, $"{ChartOfAccountsBaseUrl}/subsidiary-accounts/{id}/archive", null, cancellationToken);

    public Task<ScenarioApiResult<object?>> ReactivateSubsidiaryAccountAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<object?>(HttpMethod.Post, $"{ChartOfAccountsBaseUrl}/subsidiary-accounts/{id}/reactivate", null, cancellationToken);

    /// <param name="level">"AccountClass", "GeneralAccount", or "SubsidiaryAccount".</param>
    internal Task<ScenarioApiResult<GetAccountResponse>> GetAccountAsync(
        string level, long id, CancellationToken cancellationToken = default) =>
        SendAsync<GetAccountResponse>(HttpMethod.Get, $"{ChartOfAccountsBaseUrl}/{level}/{id}", null, cancellationToken);

    internal Task<ScenarioApiResult<IReadOnlyList<ClassNode>>> GetAccountTreeAsync(
        bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("includeArchived", includeArchived));
        return SendAsync<IReadOnlyList<ClassNode>>(HttpMethod.Get, $"{ChartOfAccountsBaseUrl}/tree{query}", null, cancellationToken);
    }

    internal Task<ScenarioApiResult<SearchAccountsResponse>> SearchAccountsAsync(
        AccountLevel? level = null, long? parentId = null, string? term = null, AccountNature? nature = null,
        DetailAccountType? detailAccountType = null, AccountStatus? status = null, int page = 1, int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("level", level), ("parentId", parentId), ("term", term), ("nature", nature),
            ("detailAccountType", detailAccountType), ("status", status), ("page", page), ("pageSize", pageSize));
        return SendAsync<SearchAccountsResponse>(HttpMethod.Get, $"{ChartOfAccountsBaseUrl}/search{query}", null, cancellationToken);
    }

    // ---------------------------------------------------------------------------------------
    // Detail Accounts
    // ---------------------------------------------------------------------------------------

    public Task<ScenarioApiResult<CreateDetailAccountResponse>> CreateDetailAccountAsync(
        string code, string name, string type, CancellationToken cancellationToken = default) =>
        SendAsync<CreateDetailAccountResponse>(HttpMethod.Post, DetailAccountsBaseUrl,
            new CreateDetailAccountRequest(code, name, type), cancellationToken);

    public Task<ScenarioApiResult<UpdateDetailAccountResponse>> UpdateDetailAccountAsync(
        long id, string name, string type, string? code = null, CancellationToken cancellationToken = default) =>
        SendAsync<UpdateDetailAccountResponse>(HttpMethod.Put, $"{DetailAccountsBaseUrl}/{id}",
            new UpdateDetailAccountRequest(name, type, code), cancellationToken);

    public Task<ScenarioApiResult<object?>> ArchiveDetailAccountAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<object?>(HttpMethod.Post, $"{DetailAccountsBaseUrl}/{id}/archive", null, cancellationToken);

    public Task<ScenarioApiResult<object?>> ReactivateDetailAccountAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<object?>(HttpMethod.Post, $"{DetailAccountsBaseUrl}/{id}/reactivate", null, cancellationToken);

    public Task<ScenarioApiResult<object?>> DeleteDetailAccountAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<object?>(HttpMethod.Delete, $"{DetailAccountsBaseUrl}/{id}", null, cancellationToken);

    public Task<ScenarioApiResult<GetDetailAccountResponse>> GetDetailAccountAsync(
        long id, CancellationToken cancellationToken = default) =>
        SendAsync<GetDetailAccountResponse>(HttpMethod.Get, $"{DetailAccountsBaseUrl}/{id}", null, cancellationToken);

    public Task<ScenarioApiResult<GetDetailAccountByCodeResponse>> GetDetailAccountByCodeAsync(
        string code, CancellationToken cancellationToken = default) =>
        SendAsync<GetDetailAccountByCodeResponse>(HttpMethod.Get, $"{DetailAccountsBaseUrl}/by-code/{code}", null, cancellationToken);

    public Task<ScenarioApiResult<ListDetailAccountsResponse>> ListDetailAccountsAsync(
        string? type = null, string? status = null, string? search = null, int page = 1, int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("type", type), ("status", status), ("search", search), ("page", page), ("pageSize", pageSize));
        return SendAsync<ListDetailAccountsResponse>(HttpMethod.Get, $"{DetailAccountsBaseUrl}{query}", null, cancellationToken);
    }

    public Task<ScenarioApiResult<SearchDetailAccountsForPostingResponse>> SearchDetailAccountsForPostingAsync(
        string type, string? search = null, int limit = 20, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("type", type), ("search", search), ("limit", limit));
        return SendAsync<SearchDetailAccountsForPostingResponse>(
            HttpMethod.Get, $"{DetailAccountsBaseUrl}/posting-search{query}", null, cancellationToken);
    }

    // ---------------------------------------------------------------------------------------
    // Journal Entries
    // ---------------------------------------------------------------------------------------

    public Task<ScenarioApiResult<JournalEntryDetailResponse>> CreateDraftEntryAsync(
        CreateDraftJournalEntryRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<JournalEntryDetailResponse>(HttpMethod.Post, JournalEntriesBaseUrl, request, cancellationToken);

    public Task<ScenarioApiResult<JournalEntryDetailResponse>> UpdateDraftEntryAsync(
        long fiscalYearId, long id, UpdateDraftJournalEntryRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<JournalEntryDetailResponse>(
            HttpMethod.Put, $"{JournalEntriesBaseUrl}/{fiscalYearId}/{id}", request, cancellationToken);

    public Task<ScenarioApiResult<JournalEntryDetailResponse>> AppendDraftLinesAsync(
        long fiscalYearId, long id, IReadOnlyList<JournalEntryLineRequest> lines,
        CancellationToken cancellationToken = default) =>
        SendAsync<JournalEntryDetailResponse>(HttpMethod.Post, $"{JournalEntriesBaseUrl}/{fiscalYearId}/{id}/lines",
            new AppendDraftLinesRequest { Lines = lines }, cancellationToken);

    public Task<ScenarioApiResult<JournalEntryDetailResponse>> ReplaceDraftLinesAsync(
        long fiscalYearId, long id, IReadOnlyList<JournalEntryLineRequest> lines,
        CancellationToken cancellationToken = default) =>
        SendAsync<JournalEntryDetailResponse>(HttpMethod.Put, $"{JournalEntriesBaseUrl}/{fiscalYearId}/{id}/lines",
            new ReplaceDraftLinesRequest { Lines = lines }, cancellationToken);

    public Task<ScenarioApiResult<JournalEntryDetailResponse>> PostEntryAsync(
        long fiscalYearId, long id, CancellationToken cancellationToken = default) =>
        SendAsync<JournalEntryDetailResponse>(
            HttpMethod.Post, $"{JournalEntriesBaseUrl}/{fiscalYearId}/{id}/post", null, cancellationToken);

    public Task<ScenarioApiResult<JournalEntryDetailResponse>> ReverseEntryAsync(
        long fiscalYearId, long originalReferenceNumber, DateOnly accountingDate, string reversalReason,
        CancellationToken cancellationToken = default) =>
        SendAsync<JournalEntryDetailResponse>(
            HttpMethod.Post, $"{JournalEntriesBaseUrl}/{fiscalYearId}/by-reference/{originalReferenceNumber}/reverse",
            new ReverseJournalEntryRequest { AccountingDate = accountingDate, ReversalReason = reversalReason },
            cancellationToken);

    public Task<ScenarioApiResult<object?>> DeleteDraftEntryAsync(
        long fiscalYearId, long id, CancellationToken cancellationToken = default) =>
        SendAsync<object?>(HttpMethod.Delete, $"{JournalEntriesBaseUrl}/{fiscalYearId}/{id}", null, cancellationToken);

    public Task<ScenarioApiResult<JournalEntryDetailResponse>> GetEntryAsync(
        long fiscalYearId, long id, CancellationToken cancellationToken = default) =>
        SendAsync<JournalEntryDetailResponse>(
            HttpMethod.Get, $"{JournalEntriesBaseUrl}/{fiscalYearId}/{id}", null, cancellationToken);

    public Task<ScenarioApiResult<JournalEntryDetailResponse>> GetEntryByReferenceAsync(
        long accountingBookId, long fiscalYearId, long referenceNumber, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("accountingBookId", accountingBookId), ("fiscalYearId", fiscalYearId), ("referenceNumber", referenceNumber));
        return SendAsync<JournalEntryDetailResponse>(
            HttpMethod.Get, $"{JournalEntriesBaseUrl}/by-reference{query}", null, cancellationToken);
    }

    public Task<ScenarioApiResult<JournalEntryDetailResponse>> GetEntryByNumberAsync(
        long accountingBookId, long fiscalYearId, long journalEntryNumber, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("accountingBookId", accountingBookId), ("fiscalYearId", fiscalYearId),
            ("journalEntryNumber", journalEntryNumber));
        return SendAsync<JournalEntryDetailResponse>(
            HttpMethod.Get, $"{JournalEntriesBaseUrl}/by-number{query}", null, cancellationToken);
    }

    public Task<ScenarioApiResult<SearchJournalEntriesResponse>> SearchJournalEntriesAsync(
        long fiscalYearId, long? accountingBookId = null, DateOnly? fromDate = null, DateOnly? toDate = null,
        long? referenceNumber = null, long? journalEntryNumber = null, string? status = null,
        string? balanceEffect = null, string? documentType = null, string? insertionType = null,
        string? accountClassCode = null, string? generalAccountCode = null, string? subsidiaryAccountCode = null,
        string? detailAccountCode = null, string? sourceType = null, string? sourceReference = null,
        int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("fiscalYearId", fiscalYearId), ("accountingBookId", accountingBookId), ("fromDate", fromDate),
            ("toDate", toDate), ("referenceNumber", referenceNumber), ("journalEntryNumber", journalEntryNumber),
            ("status", status), ("balanceEffect", balanceEffect), ("documentType", documentType),
            ("insertionType", insertionType), ("accountClassCode", accountClassCode),
            ("generalAccountCode", generalAccountCode), ("subsidiaryAccountCode", subsidiaryAccountCode),
            ("detailAccountCode", detailAccountCode), ("sourceType", sourceType), ("sourceReference", sourceReference),
            ("page", page), ("pageSize", pageSize));
        return SendAsync<SearchJournalEntriesResponse>(HttpMethod.Get, $"{JournalEntriesBaseUrl}{query}", null, cancellationToken);
    }

    public Task<ScenarioApiResult<IReadOnlyList<JournalEntryAuditItem>>> GetJournalEntryAuditAsync(
        long accountingBookId, long fiscalYearId, long referenceNumber, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("accountingBookId", accountingBookId));
        return SendAsync<IReadOnlyList<JournalEntryAuditItem>>(HttpMethod.Get,
            $"{JournalEntriesBaseUrl}/{fiscalYearId}/by-reference/{referenceNumber}/audit{query}", null, cancellationToken);
    }

    public Task<ScenarioApiResult<object?>> RebuildJournalEntryProjectionsAsync(
        long fiscalYearId, DateOnly? fromDate = null, CancellationToken cancellationToken = default) =>
        SendAsync<object?>(HttpMethod.Post, $"{JournalEntriesBaseUrl}/{fiscalYearId}/projections/rebuild",
            new RebuildJournalEntryProjectionsRequest { FromDate = fromDate }, cancellationToken);

    public Task<ScenarioApiResult<ReconcileJournalEntryProjectionsResponse>> ReconcileJournalEntryProjectionsAsync(
        long fiscalYearId, CancellationToken cancellationToken = default) =>
        SendAsync<ReconcileJournalEntryProjectionsResponse>(
            HttpMethod.Get, $"{JournalEntriesBaseUrl}/{fiscalYearId}/projections/reconcile", null, cancellationToken);

    // ---------------------------------------------------------------------------------------
    // Reports
    // ---------------------------------------------------------------------------------------

    public Task<ScenarioApiResult<IReadOnlyList<AccountReportItem>>> GetTrialBalanceAsync(
        long accountingBookId, long fiscalYearId, DateOnly fromDate, DateOnly toDate,
        IReadOnlyList<string>? excludedDocumentTypes = null, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AccountReportItem>>(HttpMethod.Post, $"{JournalEntriesBaseUrl}/reports/trial-balance",
            new GetTrialBalanceRequest
            {
                AccountingBookId = accountingBookId, FiscalYearId = fiscalYearId, FromDate = fromDate, ToDate = toDate,
                ExcludedDocumentTypes = excludedDocumentTypes ?? []
            },
            cancellationToken);

    public Task<ScenarioApiResult<IReadOnlyList<AccountReportItem>>> GetAccountBalancesAsync(
        long accountingBookId, long fiscalYearId, DateOnly asOfDate, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AccountReportItem>>(HttpMethod.Post, $"{JournalEntriesBaseUrl}/reports/balances",
            new GetAccountBalancesRequest { AccountingBookId = accountingBookId, FiscalYearId = fiscalYearId, AsOfDate = asOfDate },
            cancellationToken);

    public Task<ScenarioApiResult<IReadOnlyList<CrossFiscalYearTurnoverItem>>> GetCrossFiscalYearTurnoverAsync(
        long accountingBookId, IReadOnlyList<long> fiscalYearIds, DateOnly fromDate, DateOnly toDate,
        IReadOnlyList<string>? excludedDocumentTypes = null, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<CrossFiscalYearTurnoverItem>>(
            HttpMethod.Post, $"{JournalEntriesBaseUrl}/reports/cross-fiscal-year-turnover",
            new GetCrossFiscalYearTurnoverRequest
            {
                AccountingBookId = accountingBookId, FiscalYearIds = fiscalYearIds, FromDate = fromDate, ToDate = toDate,
                ExcludedDocumentTypes = excludedDocumentTypes ?? []
            },
            cancellationToken);

    public Task<ScenarioApiResult<IReadOnlyList<JournalTransactionItem>>> GetGeneralLedgerReportAsync(
        GetTransactionReportRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<JournalTransactionItem>>(
            HttpMethod.Post, $"{JournalEntriesBaseUrl}/reports/general-ledger", request, cancellationToken);

    public Task<ScenarioApiResult<IReadOnlyList<JournalTransactionItem>>> GetJournalReportAsync(
        GetTransactionReportRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<JournalTransactionItem>>(
            HttpMethod.Post, $"{JournalEntriesBaseUrl}/reports/journal", request, cancellationToken);

    // ---------------------------------------------------------------------------------------
    // Transport plumbing
    // ---------------------------------------------------------------------------------------

    private async Task<ScenarioApiResult<TResponse>> SendAsync<TResponse>(
        HttpMethod method, string url, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);
        if (_unauthenticated)
            request.Headers.Add("X-Test-Unauthenticated", "true");

        using var response = await _client.SendAsync(request, cancellationToken);
        var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType;

        TResponse? value = default;
        ProblemDetailsPayload? problem = null;

        if (!string.IsNullOrWhiteSpace(rawBody))
        {
            if (response.IsSuccessStatusCode)
                value = JsonSerializer.Deserialize<TResponse>(rawBody, JsonOptions);
            else
                problem = JsonSerializer.Deserialize<ProblemDetailsPayload>(rawBody, JsonOptions);
        }

        return new ScenarioApiResult<TResponse>(response.StatusCode, contentType, rawBody, value, problem);
    }

    /// <summary>
    /// Builds a query string from explicit key/value pairs. Not reflection-based — every caller
    /// lists its own parameters — kept private because it is transport plumbing, not a business
    /// operation in its own right.
    /// </summary>
    private static string BuildQuery(params (string Key, object? Value)[] parameters)
    {
        List<string>? parts = null;
        foreach (var (key, value) in parameters)
        {
            if (value is null)
                continue;
            var text = value switch
            {
                DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                bool flag => flag ? "true" : "false",
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            };
            (parts ??= []).Add($"{key}={Uri.EscapeDataString(text)}");
        }
        return parts is null ? string.Empty : "?" + string.Join('&', parts);
    }
}
