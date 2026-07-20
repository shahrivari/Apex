using Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.Reporting;

public sealed record JournalTransactionItem(
    long EntryId,
    long FiscalYearId,
    long ReferenceNumber,
    long JournalEntryNumber,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    string EntryDescription,
    string DocumentType,
    string InsertionType,
    string BalanceEffect,
    long? ReversalOfReferenceNumber,
    long? ReversedByReferenceNumber,
    string? ReversalReason,
    int RowNumber,
    string AccountClassCode,
    string GeneralAccountCode,
    string SubsidiaryAccountCode,
    string? DetailAccountCode,
    string Side,
    decimal Amount,
    string LineDescription)
{
    internal static JournalTransactionItem From(JournalTransactionRow row) => new(
        row.EntryId, row.FiscalYearId, row.ReferenceNumber, row.JournalEntryNumber,
        row.AccountingDate, row.RegisteredAt, row.EntryDescription, row.DocumentType,
        row.InsertionType, row.BalanceEffect, row.ReversalOfReferenceNumber,
        row.ReversedByReferenceNumber, row.ReversalReason, row.RowNumber,
        row.AccountClassCode, row.GeneralAccountCode, row.SubsidiaryAccountCode,
        string.IsNullOrEmpty(row.DetailAccountCode) ? null : row.DetailAccountCode,
        row.Side, row.Amount, row.LineDescription);
}
