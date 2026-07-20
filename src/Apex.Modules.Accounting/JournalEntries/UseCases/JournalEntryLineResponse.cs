using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

namespace Apex.Modules.Accounting.JournalEntries.UseCases;

public sealed record JournalEntryLineResponse(
    long Id,
    int RowNumber,
    string Side,
    decimal Amount,
    string AccountClassCode,
    string GeneralAccountCode,
    string SubsidiaryAccountCode,
    string? DetailAccountCode,
    string Description)
{
    internal static JournalEntryLineResponse From(JournalEntryLine line) => new(
        line.Id, line.RowNumber, line.Side.ToDatabaseValue(), line.Amount, line.AccountClassCode,
        line.GeneralAccountCode, line.SubsidiaryAccountCode, line.DetailAccountCode, line.Description);

    internal static JournalEntryLineResponse From(JournalEntryLineRow row) => new(
        row.Id, row.RowNumber, row.Side, row.Amount, row.AccountClassCode,
        row.GeneralAccountCode, row.SubsidiaryAccountCode, row.DetailAccountCode, row.Description);
}
