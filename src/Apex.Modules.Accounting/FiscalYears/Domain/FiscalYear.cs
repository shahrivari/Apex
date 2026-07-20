using Apex.Application.Abstractions.Exceptions;

namespace Apex.Modules.Accounting.FiscalYears.Domain;

public sealed class FiscalYear
{
    public long Id { get; private init; }
    public long AccountingBookId { get; private init; }
    public string Title { get; private set; } = null!;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public FiscalYearStatus Status { get; private set; }
    public DateOnly FinalizedThroughDate { get; private set; }
    public long NextReferenceNumber { get; private set; }
    public long NextJournalEntryNumber { get; private set; }
    public DateTime CreatedAt { get; private init; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? OpenedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateOnly? CancellationDate { get; private set; }

    private FiscalYear() { }

    public DateOnly EffectiveEndDate => CancellationDate ?? EndDate;

    public static FiscalYear Create(
        long id, long accountingBookId, string title, DateOnly startDate, DateOnly endDate, DateTime createdAt)
    {
        ValidateRange(startDate, endDate);
        if (id <= 0 || accountingBookId <= 0)
            throw new BusinessRuleException("Fiscal year identity is invalid.", FiscalYearErrors.InvalidDateRange);
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 256)
            throw new BusinessRuleException("Fiscal year title is required.", FiscalYearErrors.CannotBeUpdated);
        if (startDate == DateOnly.MinValue)
            throw new BusinessRuleException("Fiscal year start date is too early.", FiscalYearErrors.InvalidDateRange);

        return new FiscalYear
        {
            Id = id,
            AccountingBookId = accountingBookId,
            Title = title.Trim(),
            StartDate = startDate,
            EndDate = endDate,
            Status = FiscalYearStatus.Draft,
            FinalizedThroughDate = startDate.AddDays(-1),
            NextReferenceNumber = 1,
            NextJournalEntryNumber = 1,
            CreatedAt = createdAt
        };
    }

    internal static FiscalYear Rehydrate(
        long id, long accountingBookId, string title, DateOnly startDate, DateOnly endDate,
        FiscalYearStatus status, DateOnly finalizedThroughDate, long nextReferenceNumber,
        long nextJournalEntryNumber,
        DateTime createdAt, DateTime? updatedAt, DateTime? openedAt, DateTime? closedAt,
        DateTime? cancelledAt, DateOnly? cancellationDate) => new()
        {
            Id = id,
            AccountingBookId = accountingBookId,
            Title = title,
            StartDate = startDate,
            EndDate = endDate,
            Status = status,
            FinalizedThroughDate = finalizedThroughDate,
            NextReferenceNumber = nextReferenceNumber,
            NextJournalEntryNumber = nextJournalEntryNumber,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            OpenedAt = openedAt,
            ClosedAt = closedAt,
            CancelledAt = cancelledAt,
            CancellationDate = cancellationDate
        };

    public (long ReferenceNumber, long JournalEntryNumber) AllocateJournalEntryNumbers()
    {
        if (Status != FiscalYearStatus.Open
            || NextReferenceNumber == long.MaxValue
            || NextJournalEntryNumber == long.MaxValue)
            throw new BusinessRuleException(
                "The fiscal year cannot allocate journal entry numbers.",
                FiscalYearErrors.CannotAllocateNumber);

        return (NextReferenceNumber++, NextJournalEntryNumber++);
    }

    public void UpdateDraft(string title, DateOnly startDate, DateOnly endDate, DateTime now)
    {
        if (Status != FiscalYearStatus.Draft)
            throw new BusinessRuleException("Only a draft fiscal year can be updated.", FiscalYearErrors.CannotBeUpdated);
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 256)
            throw new BusinessRuleException("Fiscal year title is required.", FiscalYearErrors.CannotBeUpdated);
        ValidateRange(startDate, endDate);
        if (startDate == DateOnly.MinValue)
            throw new BusinessRuleException("Fiscal year start date is too early.", FiscalYearErrors.InvalidDateRange);

        Title = title.Trim();
        StartDate = startDate;
        EndDate = endDate;
        FinalizedThroughDate = startDate.AddDays(-1);
        UpdatedAt = now;
    }

    public void EnsureCanUpdate()
    {
        if (Status != FiscalYearStatus.Draft)
            throw new BusinessRuleException("Only a draft fiscal year can be updated.", FiscalYearErrors.CannotBeUpdated);
    }

    public void Open(DateTime now)
    {
        if (Status != FiscalYearStatus.Draft)
            throw new BusinessRuleException("Only a draft fiscal year can be opened.", FiscalYearErrors.CannotBeOpened);
        Status = FiscalYearStatus.Open;
        OpenedAt = now;
        UpdatedAt = now;
    }

    public void FinalizeThrough(DateOnly date, DateTime now)
    {
        if (Status != FiscalYearStatus.Open || date < FinalizedThroughDate || date > EffectiveEndDate)
            throw new BusinessRuleException("The fiscal year cannot be finalized through the requested date.", FiscalYearErrors.CannotBeFinalized);
        if (date == FinalizedThroughDate)
            return;
        FinalizedThroughDate = date;
        UpdatedAt = now;
    }

    public void FinalizeNextDay(DateOnly date, DateTime now)
    {
        if (FinalizedThroughDate == DateOnly.MaxValue
            || date != FinalizedThroughDate.AddDays(1))
            throw new BusinessRuleException(
                "Daily finalization must advance exactly one day.",
                FiscalYearErrors.CannotBeFinalized);
        FinalizeThrough(date, now);
    }

    public void Cancel(DateOnly cancellationDate, DateTime now)
    {
        if (Status != FiscalYearStatus.Open
            || cancellationDate < StartDate || cancellationDate > EndDate
            || cancellationDate != FinalizedThroughDate)
            throw new BusinessRuleException("The fiscal year cannot be cancelled at the requested date.", FiscalYearErrors.CannotBeCancelled);

        Status = FiscalYearStatus.Cancelled;
        CancellationDate = cancellationDate;
        CancelledAt = now;
        UpdatedAt = now;
    }

    public void EnsureCanDelete()
    {
        if (Status != FiscalYearStatus.Draft)
            throw new BusinessRuleException("Only a draft fiscal year can be deleted.", FiscalYearErrors.CannotBeDeleted);
    }

    public void EnsureDateAcceptsActivity(DateOnly date)
    {
        if (Status != FiscalYearStatus.Open || date < StartDate || date > EffectiveEndDate)
            throw new BusinessRuleException("The fiscal year is not open for the accounting date.", FiscalYearErrors.NotFoundForDate);
        if (date <= FinalizedThroughDate)
            throw new BusinessRuleException("The accounting date has been finalized.", FiscalYearErrors.DateFinalized);
    }

    private static void ValidateRange(DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate)
            throw new BusinessRuleException("Fiscal year start date must be on or before end date.", FiscalYearErrors.InvalidDateRange);
    }
}
