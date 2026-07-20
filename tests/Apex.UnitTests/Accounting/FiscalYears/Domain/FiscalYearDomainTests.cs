using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.FiscalYears.Domain;

namespace Apex.UnitTests.Accounting.FiscalYears.Domain;

public sealed class FiscalYearDomainTests
{
    private static readonly DateTime CreatedAt = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateOnly End = new(2026, 12, 31);

    [Fact]
    public void Create_InitializesDraftBoundariesAndSequence()
    {
        var fiscalYear = FiscalYear.Create(1, 2, "  2026  ", Start, End, CreatedAt);

        Assert.Equal("2026", fiscalYear.Title);
        Assert.Equal(FiscalYearStatus.Draft, fiscalYear.Status);
        Assert.Equal(new DateOnly(2025, 12, 31), fiscalYear.FinalizedThroughDate);
        Assert.Equal(1, fiscalYear.NextReferenceNumber);
        Assert.Equal(1, fiscalYear.NextJournalEntryNumber);
        Assert.Equal(CreatedAt, fiscalYear.CreatedAt);
    }

    [Fact]
    public void Create_InvalidRange_ThrowsStableError()
    {
        var exception = Assert.Throws<BusinessRuleException>(() =>
            FiscalYear.Create(1, 2, "2026", End, Start, CreatedAt));

        Assert.Equal(FiscalYearErrors.InvalidDateRange, exception.ErrorCode);
    }

    [Fact]
    public void UpdateDraft_ChangesInitialFinalizationBoundary()
    {
        var fiscalYear = CreateDraft();
        var now = CreatedAt.AddDays(1);

        fiscalYear.UpdateDraft("Updated", new DateOnly(2026, 2, 1), End, now);

        Assert.Equal(new DateOnly(2026, 1, 31), fiscalYear.FinalizedThroughDate);
        Assert.Equal(now, fiscalYear.UpdatedAt);
    }

    [Fact]
    public void Open_MakesDatesImmutable()
    {
        var fiscalYear = CreateDraft();
        fiscalYear.Open(CreatedAt.AddDays(1));

        var exception = Assert.Throws<BusinessRuleException>(() =>
            fiscalYear.UpdateDraft("Changed", Start, End, CreatedAt.AddDays(2)));

        Assert.Equal(FiscalYearErrors.CannotBeUpdated, exception.ErrorCode);
    }

    [Fact]
    public void FinalizeThrough_AdvancesAndDoesNotMoveBackward()
    {
        var fiscalYear = CreateDraft();
        fiscalYear.Open(CreatedAt.AddDays(1));
        fiscalYear.FinalizeThrough(new DateOnly(2026, 3, 31), CreatedAt.AddDays(2));

        var exception = Assert.Throws<BusinessRuleException>(() =>
            fiscalYear.FinalizeThrough(new DateOnly(2026, 3, 30), CreatedAt.AddDays(3)));

        Assert.Equal(new DateOnly(2026, 3, 31), fiscalYear.FinalizedThroughDate);
        Assert.Equal(FiscalYearErrors.CannotBeFinalized, exception.ErrorCode);
    }

    [Fact]
    public void Cancel_RequiresFinalizedThroughDateAndBecomesTerminal()
    {
        var fiscalYear = CreateDraft();
        fiscalYear.Open(CreatedAt.AddDays(1));
        var cancellationDate = new DateOnly(2026, 6, 30);
        fiscalYear.FinalizeThrough(cancellationDate, CreatedAt.AddDays(2));
        fiscalYear.Cancel(cancellationDate, CreatedAt.AddDays(3));

        Assert.Equal(FiscalYearStatus.Cancelled, fiscalYear.Status);
        Assert.Equal(cancellationDate, fiscalYear.EffectiveEndDate);
        Assert.Throws<BusinessRuleException>(() => fiscalYear.Open(CreatedAt.AddDays(4)));
    }

    [Fact]
    public void Cancel_Draft_IsRejected()
    {
        var fiscalYear = CreateDraft();
        var cancellationDate = new DateOnly(2026, 3, 31);

        var exception = Assert.Throws<BusinessRuleException>(() =>
            fiscalYear.Cancel(cancellationDate, CreatedAt.AddDays(1)));

        Assert.Equal(FiscalYearErrors.CannotBeCancelled, exception.ErrorCode);
        Assert.Equal(FiscalYearStatus.Draft, fiscalYear.Status);
    }

    [Fact]
    public void EnsureDateAcceptsActivity_RejectsFinalizedDate()
    {
        var fiscalYear = CreateDraft();
        fiscalYear.Open(CreatedAt.AddDays(1));
        fiscalYear.FinalizeThrough(new DateOnly(2026, 1, 31), CreatedAt.AddDays(2));

        var exception = Assert.Throws<BusinessRuleException>(() =>
            fiscalYear.EnsureDateAcceptsActivity(new DateOnly(2026, 1, 31)));

        Assert.Equal(FiscalYearErrors.DateFinalized, exception.ErrorCode);
    }

    [Theory]
    [InlineData(FiscalYearStatus.Draft, "DRAFT")]
    [InlineData(FiscalYearStatus.Open, "OPEN")]
    [InlineData(FiscalYearStatus.Closed, "CLOSED")]
    [InlineData(FiscalYearStatus.Cancelled, "CANCELLED")]
    public void Status_RoundTripsDatabaseValue(FiscalYearStatus status, string databaseValue)
    {
        Assert.Equal(databaseValue, status.ToDatabaseValue());
        Assert.Equal(status, FiscalYearStatusExtensions.FromDatabaseValue(databaseValue));
    }

    private static FiscalYear CreateDraft() => FiscalYear.Create(1, 2, "2026", Start, End, CreatedAt);
}
