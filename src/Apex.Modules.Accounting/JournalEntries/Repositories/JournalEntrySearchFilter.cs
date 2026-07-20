namespace Apex.Modules.Accounting.JournalEntries.Repositories;

/// <summary>
/// Search criteria for Journal Entries. <see cref="FiscalYearId"/> is required because it selects
/// the shard; the remaining fields are optional filters. Account-code fields match against the
/// entry's lines.
/// </summary>
public sealed record JournalEntrySearchFilter(
    long FiscalYearId,
    long? AccountingBookId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    long? ReferenceNumber,
    long? JournalEntryNumber,
    string? Status,
    string? BalanceEffect,
    string? DocumentType,
    string? InsertionType,
    string? AccountClassCode,
    string? GeneralAccountCode,
    string? SubsidiaryAccountCode,
    string? DetailAccountCode,
    string? SourceType,
    string? SourceReference,
    int Page,
    int PageSize);
