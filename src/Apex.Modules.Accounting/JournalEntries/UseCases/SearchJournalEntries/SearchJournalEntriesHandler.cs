using Apex.Modules.Accounting.JournalEntries.Repositories;
using Apex.Modules.Accounting.JournalEntries.Domain;
using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.SearchJournalEntries;

public sealed class SearchJournalEntriesHandler(
    IValidator<SearchJournalEntriesRequest> validator,
    IJournalEntryReadRepository readRepository)
{
    public async Task<SearchJournalEntriesResponse> HandleAsync(
        SearchJournalEntriesRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var filter = new JournalEntrySearchFilter(
            request.FiscalYearId, request.AccountingBookId, request.FromDate, request.ToDate,
            request.ReferenceNumber, request.JournalEntryNumber, NormalizeStatus(request.Status),
            NormalizeBalanceEffect(request.BalanceEffect), NormalizeDocumentType(request.DocumentType),
            NormalizeInsertionType(request.InsertionType), request.AccountClassCode, request.GeneralAccountCode,
            request.SubsidiaryAccountCode, request.DetailAccountCode,
            Normalize(request.SourceType), Normalize(request.SourceReference), request.Page, request.PageSize);

        var (items, totalCount) = await readRepository.SearchAsync(filter, cancellationToken);
        var summaries = items
            .Select(row => new JournalEntrySummary(
                row.Id, row.AccountingBookId, row.FiscalYearId, row.ReferenceNumber, row.JournalEntryNumber,
                row.NumberFinalized, row.AccountingDate, row.Description, row.DocumentType, row.InsertionType,
                row.Status, row.BalanceEffect))
            .ToList();

        return new SearchJournalEntriesResponse(summaries, totalCount, request.Page, request.PageSize);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeStatus(string? value) =>
        JournalEntryStatusExtensions.TryParse(value, out var parsed) ? parsed.ToDatabaseValue() : null;

    private static string? NormalizeBalanceEffect(string? value) =>
        BalanceEffectExtensions.TryParse(value, out var parsed) ? parsed.ToDatabaseValue() : null;

    private static string? NormalizeDocumentType(string? value) =>
        DocumentTypeExtensions.TryParse(value, out var parsed) ? parsed.ToDatabaseValue() : null;

    private static string? NormalizeInsertionType(string? value) =>
        InsertionTypeExtensions.TryParse(value, out var parsed) ? parsed.ToDatabaseValue() : null;
}
