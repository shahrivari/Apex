namespace Apex.Modules.Accounting.JournalEntries.UseCases.SearchJournalEntries;

/// <summary>
/// Journal Entry search criteria. <see cref="FiscalYearId"/> is required because it selects the
/// shard; all other fields are optional filters.
/// </summary>
public sealed class SearchJournalEntriesRequest
{
    public long FiscalYearId { get; init; }
    public long? AccountingBookId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public long? ReferenceNumber { get; init; }
    public long? JournalEntryNumber { get; init; }
    public string? Status { get; init; }
    public string? BalanceEffect { get; init; }
    public string? DocumentType { get; init; }
    public string? InsertionType { get; init; }
    public string? AccountClassCode { get; init; }
    public string? GeneralAccountCode { get; init; }
    public string? SubsidiaryAccountCode { get; init; }
    public string? DetailAccountCode { get; init; }
    public string? SourceType { get; init; }
    public string? SourceReference { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
