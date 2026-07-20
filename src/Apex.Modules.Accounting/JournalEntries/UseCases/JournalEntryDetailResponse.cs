using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

namespace Apex.Modules.Accounting.JournalEntries.UseCases;

/// <summary>
/// The full public view of a Journal Entry with its ordered lines. Returned by every use case
/// that yields a single entry (create, get, update, append lines, replace lines).
/// </summary>
public sealed record JournalEntryDetailResponse(
    long Id,
    long AccountingBookId,
    long FiscalYearId,
    long ReferenceNumber,
    long JournalEntryNumber,
    bool NumberFinalized,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    string Description,
    string DocumentType,
    string InsertionType,
    string Status,
    string BalanceEffect,
    string? SourceType,
    string? SourceReference,
    long? ReversalOfReferenceNumber,
    long? ReversedByReferenceNumber,
    string? ReversalReason,
    DateTime? PostedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<JournalEntryLineResponse> Lines)
{
    internal static JournalEntryDetailResponse From(JournalEntry entry) => new(
        entry.Id, entry.AccountingBookId, entry.FiscalYearId, entry.ReferenceNumber,
        entry.JournalEntryNumber, entry.NumberFinalized, entry.AccountingDate, entry.RegisteredAt,
        entry.Description, entry.DocumentType.ToDatabaseValue(), entry.InsertionType.ToDatabaseValue(),
        entry.Status.ToDatabaseValue(), entry.BalanceEffect.ToDatabaseValue(), entry.SourceType,
        entry.SourceReference, entry.ReversalOfReferenceNumber, entry.ReversedByReferenceNumber,
        entry.ReversalReason, entry.PostedAt, entry.CreatedAt, entry.UpdatedAt,
        entry.Lines.Select(JournalEntryLineResponse.From).ToList());

    internal static JournalEntryDetailResponse From(JournalEntryWithLines model)
    {
        var header = model.Header;
        return new JournalEntryDetailResponse(
            header.Id, header.AccountingBookId, header.FiscalYearId, header.ReferenceNumber,
            header.JournalEntryNumber, header.NumberFinalized, header.AccountingDate, header.RegisteredAt,
            header.Description, header.DocumentType, header.InsertionType, header.Status, header.BalanceEffect,
            header.SourceType, header.SourceReference, header.ReversalOfReferenceNumber,
            header.ReversedByReferenceNumber, header.ReversalReason, header.PostedAt, header.CreatedAt,
            header.UpdatedAt, model.Lines.Select(JournalEntryLineResponse.From).ToList());
    }
}
