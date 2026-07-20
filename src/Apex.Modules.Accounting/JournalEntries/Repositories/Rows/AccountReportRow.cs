namespace Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

public sealed record AccountReportRow(
    long FiscalYearId,
    string AccountClassCode,
    string GeneralAccountCode,
    string SubsidiaryAccountCode,
    string DetailAccountCode,
    decimal OpeningBalance,
    decimal DebitTurnover,
    decimal CreditTurnover,
    decimal ClosingBalance);
