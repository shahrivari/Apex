using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.JournalEntries.Domain;

namespace Apex.UnitTests.Accounting.JournalEntries.Domain;

public sealed class JournalEntryDomainTests
{
    private static readonly DateTime CreatedAt = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly AccountingDate = new(2026, 3, 1);

    [Fact]
    public void Create_AssignsContiguousRowNumbersAndNormalizes()
    {
        var entry = CreateDraft(
            "  Opening balances  ",
            Line(11, JournalEntrySide.Debit, 100m),
            Line(12, JournalEntrySide.Credit, 100m));

        Assert.Equal(JournalEntryStatus.Draft, entry.Status);
        Assert.False(entry.NumberFinalized);
        Assert.Equal("Opening balances", entry.Description);
        Assert.Equal([1, 2], entry.Lines.Select(l => l.RowNumber).ToArray());
        Assert.Equal(11, entry.Lines[0].Id);
        Assert.Equal(1, entry.ReferenceNumber);
        Assert.Equal(CreatedAt, entry.CreatedAt);
    }

    [Fact]
    public void Create_WithoutLines_IsRejected()
    {
        var exception = Assert.Throws<BusinessRuleException>(() => JournalEntry.Create(
            1, 2, 3, 1, 1, AccountingDate, CreatedAt, "desc", DocumentType.General,
            InsertionType.Manual, BalanceEffect.Financial, null, null, [], CreatedAt));

        Assert.Equal(JournalEntryErrors.InsufficientLines, exception.ErrorCode);
    }

    [Fact]
    public void Create_BlankDescription_IsRejected()
    {
        var exception = Assert.Throws<BusinessRuleException>(() =>
            CreateDraft("   ", Line(11, JournalEntrySide.Debit, 100m)));

        Assert.Equal(JournalEntryErrors.DescriptionRequired, exception.ErrorCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_NonPositiveLineAmount_IsRejected(decimal amount)
    {
        var exception = Assert.Throws<BusinessRuleException>(() =>
            CreateDraft("desc", Line(11, JournalEntrySide.Debit, amount)));

        Assert.Equal(JournalEntryErrors.NonPositiveAmount, exception.ErrorCode);
    }

    [Fact]
    public void Create_IncompleteAccountPath_IsRejected()
    {
        var exception = Assert.Throws<BusinessRuleException>(() => CreateDraft(
            "desc",
            new JournalEntryLineInput(11, JournalEntrySide.Debit, 10m, "1", " ", "01", null, "line")));

        Assert.Equal(JournalEntryErrors.InvalidAccountCodePath, exception.ErrorCode);
    }

    [Fact]
    public void AppendLines_AssignsNextRowNumberByDefault()
    {
        var entry = CreateDraft("desc", Line(11, JournalEntrySide.Debit, 100m));

        entry.AppendLines([Line(12, JournalEntrySide.Credit, 60m), Line(13, JournalEntrySide.Credit, 40m)],
            CreatedAt.AddMinutes(5));

        Assert.Equal([1, 2, 3], entry.Lines.Select(l => l.RowNumber).ToArray());
        Assert.Equal(CreatedAt.AddMinutes(5), entry.UpdatedAt);
    }

    [Fact]
    public void AppendLines_DuplicateExplicitRowNumber_IsRejected()
    {
        var entry = CreateDraft("desc", Line(11, JournalEntrySide.Debit, 100m));

        var exception = Assert.Throws<BusinessRuleException>(() =>
            entry.AppendLines([Line(12, JournalEntrySide.Credit, 100m, rowNumber: 1)], CreatedAt.AddMinutes(1)));

        Assert.Equal(JournalEntryErrors.DuplicateRowNumber, exception.ErrorCode);
    }

    [Fact]
    public void ReplaceLines_ReassignsContiguousRowNumbers()
    {
        var entry = CreateDraft("desc",
            Line(11, JournalEntrySide.Debit, 100m), Line(12, JournalEntrySide.Credit, 100m));

        entry.ReplaceLines(
            [Line(21, JournalEntrySide.Debit, 30m), Line(22, JournalEntrySide.Debit, 20m),
                Line(23, JournalEntrySide.Credit, 50m)],
            CreatedAt.AddMinutes(10));

        Assert.Equal([1, 2, 3], entry.Lines.Select(l => l.RowNumber).ToArray());
        Assert.Equal([21, 22, 23], entry.Lines.Select(l => l.Id).ToArray());
    }

    [Fact]
    public void UpdateHeader_OnPostedEntry_IsRejected()
    {
        var posted = RehydratePosted();

        var exception = Assert.Throws<BusinessRuleException>(() => posted.UpdateHeader(
            AccountingDate, "changed", DocumentType.General, BalanceEffect.Financial, CreatedAt.AddDays(1)));

        Assert.Equal(JournalEntryErrors.DraftRequired, exception.ErrorCode);
    }

    [Fact]
    public void UpdateHeader_OnDraft_ChangesMutableFields()
    {
        var entry = CreateDraft("desc", Line(11, JournalEntrySide.Debit, 100m));
        var newDate = new DateOnly(2026, 3, 5);

        entry.UpdateHeader(newDate, "updated", DocumentType.Opening, BalanceEffect.Statistical, CreatedAt.AddHours(1));

        Assert.Equal(newDate, entry.AccountingDate);
        Assert.Equal("updated", entry.Description);
        Assert.Equal(DocumentType.Opening, entry.DocumentType);
        Assert.Equal(BalanceEffect.Statistical, entry.BalanceEffect);
        Assert.Equal(CreatedAt.AddHours(1), entry.UpdatedAt);
    }

    [Theory]
    [InlineData(DocumentType.General, "GENERAL")]
    [InlineData(DocumentType.TemporaryAccountsClosing, "TEMPORARY_ACCOUNTS_CLOSING")]
    [InlineData(DocumentType.PerformanceAccountsClosing, "PERFORMANCE_ACCOUNTS_CLOSING")]
    public void DocumentType_RoundTripsDatabaseValue(DocumentType type, string databaseValue)
    {
        Assert.Equal(databaseValue, type.ToDatabaseValue());
        Assert.Equal(type, DocumentTypeExtensions.FromDatabaseValue(databaseValue));
        Assert.True(DocumentTypeExtensions.TryParse(databaseValue.ToLowerInvariant(), out var parsed));
        Assert.Equal(type, parsed);
    }

    [Fact]
    public void Enums_TryParse_RejectsUnknownValues()
    {
        Assert.False(JournalEntrySideExtensions.TryParse("SIDEWAYS", out _));
        Assert.False(InsertionTypeExtensions.TryParse("", out _));
        Assert.False(BalanceEffectExtensions.TryParse(null, out _));
    }

    [Theory]
    [InlineData(JournalEntrySide.Debit, "DEBIT")]
    [InlineData(JournalEntrySide.Credit, "CREDIT")]
    public void Side_RoundTripsDatabaseValue(JournalEntrySide side, string databaseValue)
    {
        Assert.Equal(databaseValue, side.ToDatabaseValue());
        Assert.Equal(side, JournalEntrySideExtensions.FromDatabaseValue(databaseValue));
    }

    [Fact]
    public void Post_BalancedDraft_TransitionsToPosted()
    {
        var entry = CreateDraft("desc",
            Line(11, JournalEntrySide.Debit, 100m), Line(12, JournalEntrySide.Credit, 100m));
        var postedAt = CreatedAt.AddHours(2);

        entry.Post(postedAt);

        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
        Assert.Equal(postedAt, entry.PostedAt);
        Assert.Equal(postedAt, entry.UpdatedAt);
        Assert.Equal(100m, entry.TotalDebit());
        Assert.Equal(100m, entry.TotalCredit());
    }

    [Fact]
    public void Post_UnbalancedDraft_IsRejected()
    {
        var entry = CreateDraft("desc",
            Line(11, JournalEntrySide.Debit, 100m), Line(12, JournalEntrySide.Credit, 60m));

        var exception = Assert.Throws<BusinessRuleException>(() => entry.Post(CreatedAt.AddHours(1)));

        Assert.Equal(JournalEntryErrors.Unbalanced, exception.ErrorCode);
        Assert.Equal(JournalEntryStatus.Draft, entry.Status);
    }

    [Fact]
    public void Post_SingleLineDraft_IsRejected()
    {
        var entry = CreateDraft("desc", Line(11, JournalEntrySide.Debit, 100m));

        var exception = Assert.Throws<BusinessRuleException>(() => entry.Post(CreatedAt.AddHours(1)));

        Assert.Equal(JournalEntryErrors.InsufficientLines, exception.ErrorCode);
    }

    [Fact]
    public void Post_AlreadyPosted_IsRejected()
    {
        var posted = RehydratePosted();

        var exception = Assert.Throws<BusinessRuleException>(() => posted.Post(CreatedAt.AddHours(1)));

        Assert.Equal(JournalEntryErrors.DraftRequired, exception.ErrorCode);
    }

    [Fact]
    public void CreatePostedReversal_SwapsSidesAndLinksEntries()
    {
        var original = RehydratePosted();
        var reversedAt = AccountingDate.AddDays(1);

        var reversal = JournalEntry.CreatePostedReversal(
            original, 2, 2, 2, reversedAt, "Correction",
            [21, 22],
            CreatedAt.AddDays(1));

        Assert.Equal(JournalEntryStatus.Posted, reversal.Status);
        Assert.Equal(InsertionType.System, reversal.InsertionType);
        Assert.Equal(original.ReferenceNumber, reversal.ReversalOfReferenceNumber);
        Assert.Equal(reversal.ReferenceNumber, original.ReversedByReferenceNumber);
        Assert.Equal("Correction", reversal.ReversalReason);
        Assert.Equal([JournalEntrySide.Credit, JournalEntrySide.Debit],
            reversal.Lines.Select(line => line.Side).ToArray());
    }

    [Fact]
    public void CreatePostedReversal_WhenAlreadyReversed_IsRejected()
    {
        var original = RehydratePosted();
        _ = JournalEntry.CreatePostedReversal(
            original, 2, 2, 2, AccountingDate, "First",
            [21, 22],
            CreatedAt.AddHours(1));

        var exception = Assert.Throws<ConflictException>(() => JournalEntry.CreatePostedReversal(
            original, 3, 3, 3, AccountingDate, "Second",
            [31, 32],
            CreatedAt.AddHours(2)));

        Assert.Equal(JournalEntryErrors.AlreadyReversed, exception.ErrorCode);
    }

    [Fact]
    public void CreatePostedReversal_FromDraft_IsRejected()
    {
        var draft = CreateDraft("draft",
            Line(11, JournalEntrySide.Debit, 100m), Line(12, JournalEntrySide.Credit, 100m));

        var exception = Assert.Throws<BusinessRuleException>(() => JournalEntry.CreatePostedReversal(
            draft, 2, 2, 2, AccountingDate, "Correction", [21, 22], CreatedAt.AddHours(1)));

        Assert.Equal(JournalEntryErrors.PostedImmutable, exception.ErrorCode);
    }

    [Fact]
    public void CreatePostedReversal_BeforeOriginalDate_IsRejected()
    {
        var original = RehydratePosted();

        var exception = Assert.Throws<BusinessRuleException>(() => JournalEntry.CreatePostedReversal(
            original, 2, 2, 2, AccountingDate.AddDays(-1), "Correction", [21, 22], CreatedAt.AddHours(1)));

        Assert.Equal(JournalEntryErrors.InvalidReversalDate, exception.ErrorCode);
        Assert.Null(original.ReversedByReferenceNumber);
    }

    [Fact]
    public void CreatePostedReversal_WithoutReason_IsRejected()
    {
        var original = RehydratePosted();

        var exception = Assert.Throws<BusinessRuleException>(() => JournalEntry.CreatePostedReversal(
            original, 2, 2, 2, AccountingDate, "  ", [21, 22], CreatedAt.AddHours(1)));

        Assert.Equal(JournalEntryErrors.ReversalReasonRequired, exception.ErrorCode);
        Assert.Null(original.ReversedByReferenceNumber);
    }

    private static JournalEntry CreateDraft(string description, params JournalEntryLineInput[] lines) =>
        JournalEntry.Create(
            1, 2, 3, referenceNumber: 1, journalEntryNumber: 1, AccountingDate, CreatedAt, description,
            DocumentType.General, InsertionType.Manual, BalanceEffect.Financial, null, null, lines, CreatedAt);

    private static JournalEntryLineInput Line(
        long id, JournalEntrySide side, decimal amount, string description = "line",
        int? rowNumber = null, string detail = "D-1") =>
        new(id, side, amount, "1", "01", "01", detail, description, rowNumber);

    private static JournalEntry RehydratePosted() => JournalEntry.Rehydrate(
        1, 2, 3, 1, 1, false, AccountingDate, CreatedAt, "posted", DocumentType.General,
        InsertionType.Manual, JournalEntryStatus.Posted, BalanceEffect.Financial, null, null, null, null, null,
        CreatedAt, CreatedAt, null, [
            JournalEntryLine.Rehydrate(11, 1, JournalEntrySide.Debit, 100m, "1", "01", "01", "D-1", "line"),
            JournalEntryLine.Rehydrate(12, 2, JournalEntrySide.Credit, 100m, "1", "01", "01", "D-1", "line")
        ]);
}
