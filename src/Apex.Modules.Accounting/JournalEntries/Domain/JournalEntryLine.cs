using Apex.Application.Abstractions.Exceptions;

namespace Apex.Modules.Accounting.JournalEntries.Domain;

public sealed class JournalEntryLine
{
    public long Id { get; private init; }
    public int RowNumber { get; private set; }
    public JournalEntrySide Side { get; private init; }
    public decimal Amount { get; private init; }
    public string AccountClassCode { get; private init; } = null!;
    public string GeneralAccountCode { get; private init; } = null!;
    public string SubsidiaryAccountCode { get; private init; } = null!;
    public string? DetailAccountCode { get; private init; }
    public string Description { get; private init; } = null!;

    private JournalEntryLine() { }

    internal static JournalEntryLine Create(
        long id, int rowNumber, JournalEntrySide side, decimal amount,
        string accountClassCode, string generalAccountCode, string subsidiaryAccountCode,
        string? detailAccountCode, string description)
    {
        if (id <= 0)
            throw new BusinessRuleException("Journal entry line identity is invalid.", JournalEntryErrors.NotFound);
        if (rowNumber < 1)
            throw new BusinessRuleException("Row number must be positive.", JournalEntryErrors.InvalidRowNumber);
        if (amount <= 0)
            throw new BusinessRuleException(
                "Line amount must be greater than zero.", JournalEntryErrors.NonPositiveAmount);
        ValidateCode(accountClassCode);
        ValidateCode(generalAccountCode);
        ValidateCode(subsidiaryAccountCode);
        if (string.IsNullOrWhiteSpace(description))
            throw new BusinessRuleException("Line description is required.", JournalEntryErrors.DescriptionRequired);

        return new JournalEntryLine
        {
            Id = id,
            RowNumber = rowNumber,
            Side = side,
            Amount = amount,
            AccountClassCode = accountClassCode.Trim(),
            GeneralAccountCode = generalAccountCode.Trim(),
            SubsidiaryAccountCode = subsidiaryAccountCode.Trim(),
            DetailAccountCode = string.IsNullOrWhiteSpace(detailAccountCode) ? null : detailAccountCode.Trim(),
            Description = description.Trim()
        };
    }

    internal static JournalEntryLine Rehydrate(
        long id, int rowNumber, JournalEntrySide side, decimal amount,
        string accountClassCode, string generalAccountCode, string subsidiaryAccountCode,
        string? detailAccountCode, string description) => new()
        {
            Id = id,
            RowNumber = rowNumber,
            Side = side,
            Amount = amount,
            AccountClassCode = accountClassCode,
            GeneralAccountCode = generalAccountCode,
            SubsidiaryAccountCode = subsidiaryAccountCode,
            DetailAccountCode = detailAccountCode,
            Description = description
        };

    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new BusinessRuleException(
                "The account-code path is incomplete.", JournalEntryErrors.InvalidAccountCodePath);
    }
}
