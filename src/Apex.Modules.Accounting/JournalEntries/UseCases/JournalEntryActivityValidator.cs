using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.AccountingBooks.Repositories;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using Apex.Modules.Accounting.JournalEntries.Domain;

namespace Apex.Modules.Accounting.JournalEntries.UseCases;

public sealed class JournalEntryActivityValidator(
    IAccountingBookReadRepository accountingBookRepository,
    IFiscalYearWriteRepository fiscalYearRepository)
{
    public async Task<FiscalYear> ValidateAsync(IShardConnection shard, long fiscalYearId,
        long accountingBookId, DateOnly accountingDate, CancellationToken cancellationToken = default)
    {
        var book = await accountingBookRepository.GetByIdAsync(accountingBookId, cancellationToken)
            ?? throw new NotFoundException(
                "Accounting book was not found.", JournalEntryErrors.AccountingBookNotEligible);
        if (book.Status != "ACTIVE")
            throw new BusinessRuleException(
                "The accounting book is not active.", JournalEntryErrors.AccountingBookNotEligible);

        var fiscalYear = await fiscalYearRepository.GetByIdForUpdateAsync(
            shard, fiscalYearId, cancellationToken)
            ?? throw new NotFoundException("Fiscal year was not found.", JournalEntryErrors.FiscalYearNotFound);
        if (fiscalYear.AccountingBookId != accountingBookId)
            throw new BusinessRuleException(
                "The fiscal year belongs to another accounting book.",
                JournalEntryErrors.AccountingDateOutsideFiscalYear);
        if (fiscalYear.Status != FiscalYearStatus.Open)
            throw new BusinessRuleException(
                "The fiscal year is not open for journal activity.", JournalEntryErrors.FiscalYearNotOpen);
        if (accountingDate < fiscalYear.StartDate || accountingDate > fiscalYear.EffectiveEndDate)
            throw new BusinessRuleException(
                "The accounting date is outside the fiscal year.",
                JournalEntryErrors.AccountingDateOutsideFiscalYear);
        if (accountingDate <= fiscalYear.FinalizedThroughDate)
            throw new BusinessRuleException(
                "The accounting date has been finalized.", JournalEntryErrors.AccountingDateFinalized);
        return fiscalYear;
    }
}
