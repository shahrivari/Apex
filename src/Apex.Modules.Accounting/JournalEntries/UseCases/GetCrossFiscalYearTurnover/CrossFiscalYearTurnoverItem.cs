namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetCrossFiscalYearTurnover;

public sealed record CrossFiscalYearTurnoverItem(
    string AccountClassCode,
    string GeneralAccountCode,
    string SubsidiaryAccountCode,
    string? DetailAccountCode,
    decimal DebitTurnover,
    decimal CreditTurnover,
    decimal NetTurnover);
