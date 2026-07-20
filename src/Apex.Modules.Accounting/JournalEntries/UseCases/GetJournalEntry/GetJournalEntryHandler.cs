using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetJournalEntry;

public sealed class GetJournalEntryHandler(IJournalEntryReadRepository readRepository)
{
    public async Task<JournalEntryDetailResponse> GetByIdAsync(
        long fiscalYearId, long id, CancellationToken cancellationToken = default)
    {
        var model = await readRepository.GetByIdAsync(fiscalYearId, id, cancellationToken)
            ?? throw new NotFoundException("Journal entry was not found.", JournalEntryErrors.NotFound);
        return JournalEntryDetailResponse.From(model);
    }

    public async Task<JournalEntryDetailResponse> GetByReferenceNumberAsync(
        long accountingBookId, long fiscalYearId, long referenceNumber,
        CancellationToken cancellationToken = default)
    {
        var model = await readRepository.GetByReferenceNumberAsync(
                accountingBookId, fiscalYearId, referenceNumber, cancellationToken)
            ?? throw new NotFoundException("Journal entry was not found.", JournalEntryErrors.NotFound);
        return JournalEntryDetailResponse.From(model);
    }

    public async Task<JournalEntryDetailResponse> GetByJournalEntryNumberAsync(
        long accountingBookId, long fiscalYearId, long journalEntryNumber,
        CancellationToken cancellationToken = default)
    {
        var model = await readRepository.GetByJournalEntryNumberAsync(
                accountingBookId, fiscalYearId, journalEntryNumber, cancellationToken)
            ?? throw new NotFoundException("Journal entry was not found.", JournalEntryErrors.NotFound);
        return JournalEntryDetailResponse.From(model);
    }
}
