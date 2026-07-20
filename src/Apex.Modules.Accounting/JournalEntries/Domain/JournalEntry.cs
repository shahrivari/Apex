using Apex.Application.Abstractions.Exceptions;

namespace Apex.Modules.Accounting.JournalEntries.Domain;

public sealed class JournalEntry
{
    private readonly List<JournalEntryLine> _lines = [];

    public long Id { get; private init; }
    public long AccountingBookId { get; private init; }
    public long FiscalYearId { get; private init; }
    public long ReferenceNumber { get; private init; }
    public long JournalEntryNumber { get; private set; }
    public bool NumberFinalized { get; private set; }
    public DateOnly AccountingDate { get; private set; }
    public DateTime RegisteredAt { get; private init; }
    public string Description { get; private set; } = null!;
    public DocumentType DocumentType { get; private set; }
    public InsertionType InsertionType { get; private init; }
    public JournalEntryStatus Status { get; private set; }
    public BalanceEffect BalanceEffect { get; private set; }
    public string? SourceType { get; private init; }
    public string? SourceReference { get; private init; }
    public long? ReversalOfReferenceNumber { get; private set; }
    public long? ReversedByReferenceNumber { get; private set; }
    public string? ReversalReason { get; private set; }
    public DateTime? PostedAt { get; private set; }
    public DateTime CreatedAt { get; private init; }
    public DateTime? UpdatedAt { get; private set; }

    public IReadOnlyList<JournalEntryLine> Lines => _lines;

    private JournalEntry() { }

    public static JournalEntry Create(
        long id, long accountingBookId, long fiscalYearId, long referenceNumber, long journalEntryNumber,
        DateOnly accountingDate, DateTime registeredAt, string description, DocumentType documentType,
        InsertionType insertionType, BalanceEffect balanceEffect, string? sourceType, string? sourceReference,
        IReadOnlyList<JournalEntryLineInput> lines, DateTime createdAt)
    {
        if (id <= 0 || accountingBookId <= 0 || fiscalYearId <= 0)
            throw new BusinessRuleException("Journal entry identity is invalid.", JournalEntryErrors.NotFound);
        if (referenceNumber < 1 || journalEntryNumber < 1)
            throw new BusinessRuleException(
                "Journal entry numbering is invalid.", JournalEntryErrors.NumberingConflict);
        ValidateDescription(description);
        if (lines is null || lines.Count == 0)
            throw new BusinessRuleException(
                "A draft journal entry must have at least one line.", JournalEntryErrors.InsufficientLines);

        var entry = new JournalEntry
        {
            Id = id,
            AccountingBookId = accountingBookId,
            FiscalYearId = fiscalYearId,
            ReferenceNumber = referenceNumber,
            JournalEntryNumber = journalEntryNumber,
            NumberFinalized = false,
            AccountingDate = accountingDate,
            RegisteredAt = registeredAt,
            Description = description.Trim(),
            DocumentType = documentType,
            InsertionType = insertionType,
            Status = JournalEntryStatus.Draft,
            BalanceEffect = balanceEffect,
            SourceType = Normalize(sourceType),
            SourceReference = Normalize(sourceReference),
            CreatedAt = createdAt
        };
        entry.SetLinesContiguous(lines);
        return entry;
    }

    internal static JournalEntry Rehydrate(
        long id, long accountingBookId, long fiscalYearId, long referenceNumber, long journalEntryNumber,
        bool numberFinalized, DateOnly accountingDate, DateTime registeredAt, string description,
        DocumentType documentType, InsertionType insertionType, JournalEntryStatus status,
        BalanceEffect balanceEffect, string? sourceType, string? sourceReference,
        long? reversalOfReferenceNumber, long? reversedByReferenceNumber, string? reversalReason,
        DateTime? postedAt, DateTime createdAt, DateTime? updatedAt, IEnumerable<JournalEntryLine> lines)
    {
        var entry = new JournalEntry
        {
            Id = id,
            AccountingBookId = accountingBookId,
            FiscalYearId = fiscalYearId,
            ReferenceNumber = referenceNumber,
            JournalEntryNumber = journalEntryNumber,
            NumberFinalized = numberFinalized,
            AccountingDate = accountingDate,
            RegisteredAt = registeredAt,
            Description = description,
            DocumentType = documentType,
            InsertionType = insertionType,
            Status = status,
            BalanceEffect = balanceEffect,
            SourceType = sourceType,
            SourceReference = sourceReference,
            ReversalOfReferenceNumber = reversalOfReferenceNumber,
            ReversedByReferenceNumber = reversedByReferenceNumber,
            ReversalReason = reversalReason,
            PostedAt = postedAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
        entry._lines.AddRange(lines.OrderBy(line => line.RowNumber));
        return entry;
    }

    public void UpdateHeader(
        DateOnly accountingDate, string description, DocumentType documentType,
        BalanceEffect balanceEffect, DateTime now)
    {
        EnsureDraft();
        ValidateDescription(description);
        AccountingDate = accountingDate;
        Description = description.Trim();
        DocumentType = documentType;
        BalanceEffect = balanceEffect;
        UpdatedAt = now;
    }

    public void AppendLines(IReadOnlyList<JournalEntryLineInput> inputs, DateTime now)
    {
        EnsureDraft();
        if (inputs is null || inputs.Count == 0)
            throw new BusinessRuleException("No lines were supplied to append.", JournalEntryErrors.InsufficientLines);

        var used = _lines.Select(line => line.RowNumber).ToHashSet();
        var max = _lines.Count == 0 ? 0 : _lines.Max(line => line.RowNumber);
        foreach (var input in inputs)
        {
            int rowNumber;
            if (input.RowNumber.HasValue)
            {
                rowNumber = input.RowNumber.Value;
                if (rowNumber < 1)
                    throw new BusinessRuleException("Row number must be positive.", JournalEntryErrors.InvalidRowNumber);
                if (!used.Add(rowNumber))
                    throw new BusinessRuleException(
                        "Row number is already used.", JournalEntryErrors.DuplicateRowNumber);
                max = Math.Max(max, rowNumber);
            }
            else
            {
                rowNumber = ++max;
                used.Add(rowNumber);
            }

            _lines.Add(CreateLine(input, rowNumber));
        }

        _lines.Sort((a, b) => a.RowNumber.CompareTo(b.RowNumber));
        UpdatedAt = now;
    }

    public void ReplaceLines(IReadOnlyList<JournalEntryLineInput> inputs, DateTime now)
    {
        EnsureDraft();
        if (inputs is null || inputs.Count == 0)
            throw new BusinessRuleException(
                "A draft journal entry must have at least one line.", JournalEntryErrors.InsufficientLines);
        SetLinesContiguous(inputs);
        UpdatedAt = now;
    }

    public void EnsureDraft()
    {
        if (Status != JournalEntryStatus.Draft)
            throw new BusinessRuleException(
                "Only a draft journal entry can be modified.", JournalEntryErrors.DraftRequired);
    }

    /// <summary>
    /// Transitions the entry from draft to posted after enforcing the posting invariants that the
    /// domain owns: at least two lines and balanced debit/credit totals (required for both
    /// financial and statistical entries). Database-dependent checks (account paths, detail
    /// accounts, fiscal-year eligibility) are enforced by the posting handler.
    /// </summary>
    public void Post(DateTime now)
    {
        EnsureDraft();
        if (_lines.Count < 2)
            throw new BusinessRuleException(
                "A posted journal entry must have at least two lines.", JournalEntryErrors.InsufficientLines);
        if (TotalDebit() != TotalCredit())
            throw new BusinessRuleException(
                "Total debit must equal total credit.", JournalEntryErrors.Unbalanced);

        Status = JournalEntryStatus.Posted;
        PostedAt = now;
        UpdatedAt = now;
    }

    public static JournalEntry CreatePostedReversal(
        JournalEntry original, long id, long referenceNumber, long journalEntryNumber,
        DateOnly accountingDate, string reversalReason, IReadOnlyList<long> lineIds,
        DateTime now)
    {
        if (original.Status != JournalEntryStatus.Posted)
            throw new BusinessRuleException(
                "Only a posted journal entry can be reversed.", JournalEntryErrors.PostedImmutable);
        if (original.ReversedByReferenceNumber.HasValue)
            throw new ConflictException(
                "The journal entry has already been reversed.", JournalEntryErrors.AlreadyReversed);
        if (accountingDate < original.AccountingDate)
            throw new BusinessRuleException(
                "The reversal date cannot precede the original accounting date.",
                JournalEntryErrors.InvalidReversalDate);
        if (string.IsNullOrWhiteSpace(reversalReason))
            throw new BusinessRuleException(
                "A reversal reason is required.", JournalEntryErrors.ReversalReasonRequired);
        if (lineIds.Count != original.Lines.Count)
            throw new ArgumentException("A new identity is required for every reversal line.", nameof(lineIds));

        var lines = original.Lines.Select((line, index) => new JournalEntryLineInput(
            lineIds[index],
            line.Side == JournalEntrySide.Debit ? JournalEntrySide.Credit : JournalEntrySide.Debit,
            line.Amount,
            line.AccountClassCode,
            line.GeneralAccountCode,
            line.SubsidiaryAccountCode,
            line.DetailAccountCode,
            line.Description,
            line.RowNumber)).ToList();

        var reversal = Create(
            id, original.AccountingBookId, original.FiscalYearId, referenceNumber,
            journalEntryNumber, accountingDate, now, original.Description,
            original.DocumentType, InsertionType.System, original.BalanceEffect,
            null, null, lines, now);
        reversal.ReversalOfReferenceNumber = original.ReferenceNumber;
        reversal.ReversalReason = reversalReason.Trim();
        reversal.Post(now);
        original.ReversedByReferenceNumber = reversal.ReferenceNumber;
        original.UpdatedAt = now;
        return reversal;
    }

    public decimal TotalDebit() =>
        _lines.Where(line => line.Side == JournalEntrySide.Debit).Sum(line => line.Amount);

    public decimal TotalCredit() =>
        _lines.Where(line => line.Side == JournalEntrySide.Credit).Sum(line => line.Amount);

    private void SetLinesContiguous(IReadOnlyList<JournalEntryLineInput> inputs)
    {
        _lines.Clear();
        var rowNumber = 0;
        foreach (var input in inputs)
        {
            rowNumber++;
            _lines.Add(CreateLine(input, rowNumber));
        }
    }

    private static JournalEntryLine CreateLine(JournalEntryLineInput input, int rowNumber) =>
        JournalEntryLine.Create(
            input.Id, rowNumber, input.Side, input.Amount, input.AccountClassCode,
            input.GeneralAccountCode, input.SubsidiaryAccountCode, input.DetailAccountCode, input.Description);

    private static void ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new BusinessRuleException(
                "A description is required.", JournalEntryErrors.DescriptionRequired);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
