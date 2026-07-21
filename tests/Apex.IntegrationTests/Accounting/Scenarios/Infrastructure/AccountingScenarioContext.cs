using Apex.Modules.Accounting.JournalEntries.UseCases;

namespace Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;

/// <summary>
/// Plain mutable state for exactly one scenario run. Owned by a single
/// <see cref="AccountingScenario"/> instance — never static, never shared between tests. Journal
/// Entries are indexed by Reference Number, the stable business key, rather than the surrogate
/// database id (spec §6.1: "Prefer Reference Number over provisional Journal Entry Number").
/// </summary>
public sealed class AccountingScenarioContext
{
    public long BookId { get; set; }
    public string BookCode { get; set; } = string.Empty;

    public long FiscalYearId { get; set; }
    public string FiscalYearTitle { get; set; } = string.Empty;
    public DateOnly FiscalYearStartDate { get; set; }
    public DateOnly FiscalYearEndDate { get; set; }

    public HashSet<string> AccountClassCodes { get; } = new(StringComparer.Ordinal);
    public HashSet<string> GeneralAccountCodes { get; } = new(StringComparer.Ordinal);
    public HashSet<string> SubsidiaryAccountCodes { get; } = new(StringComparer.Ordinal);
    public HashSet<string> DetailAccountCodes { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, long> AccountClassIdsByCode { get; } = new(StringComparer.Ordinal);
    public Dictionary<(string ClassCode, string GeneralCode), long> GeneralAccountIdsByCode { get; } = new();
    public Dictionary<(string ClassCode, string GeneralCode, string SubsidiaryCode), long>
        SubsidiaryAccountIdsByCode
    { get; } = new();

    /// <summary>Journal Entries created so far, keyed by Reference Number.</summary>
    public Dictionary<long, JournalEntryDetailResponse> EntriesByReferenceNumber { get; } = new();

    public void RecordAccountClass(string code, long id)
    {
        AccountClassCodes.Add(code);
        AccountClassIdsByCode[code] = id;
    }

    public void RecordGeneralAccount(string classCode, string generalCode, long id)
    {
        GeneralAccountCodes.Add(generalCode);
        GeneralAccountIdsByCode[(classCode, generalCode)] = id;
    }

    public void RecordSubsidiaryAccount(string classCode, string generalCode, string subsidiaryCode, long id)
    {
        SubsidiaryAccountCodes.Add(subsidiaryCode);
        SubsidiaryAccountIdsByCode[(classCode, generalCode, subsidiaryCode)] = id;
    }

    public void RecordDetailAccount(string code) => DetailAccountCodes.Add(code);

    public void RecordEntry(JournalEntryDetailResponse entry) =>
        EntriesByReferenceNumber[entry.ReferenceNumber] = entry;
}
