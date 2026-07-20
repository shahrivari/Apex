using Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.Reporting;

public sealed record AccountReportItem(
    long FiscalYearId,
    string AccountClassCode,
    string GeneralAccountCode,
    string SubsidiaryAccountCode,
    string? DetailAccountCode,
    decimal OpeningBalance,
    decimal DebitTurnover,
    decimal CreditTurnover,
    decimal ClosingBalance,
    decimal DebitClosing,
    decimal CreditClosing)
{
    internal static AccountReportItem From(AccountReportRow row) => new(
        row.FiscalYearId, row.AccountClassCode, row.GeneralAccountCode,
        row.SubsidiaryAccountCode,
        string.IsNullOrEmpty(row.DetailAccountCode) ? null : row.DetailAccountCode,
        row.OpeningBalance, row.DebitTurnover, row.CreditTurnover, row.ClosingBalance,
        Math.Max(row.ClosingBalance, 0), Math.Abs(Math.Min(row.ClosingBalance, 0)));
}
