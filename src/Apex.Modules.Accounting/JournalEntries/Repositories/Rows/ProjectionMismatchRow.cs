namespace Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

public sealed record ProjectionMismatchRow(
    string Projection,
    DateOnly BalanceDate,
    string AccountClassCode,
    string GeneralAccountCode,
    string SubsidiaryAccountCode,
    string DetailAccountCode,
    string? DocumentType,
    decimal? ExpectedDebit,
    decimal? ActualDebit,
    decimal? ExpectedCredit,
    decimal? ActualCredit,
    decimal ExpectedNet,
    decimal ActualNet);
