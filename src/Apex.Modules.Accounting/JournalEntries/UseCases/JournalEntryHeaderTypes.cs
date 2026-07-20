using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.JournalEntries.Domain;

namespace Apex.Modules.Accounting.JournalEntries.UseCases;

/// <summary>
/// Parses request-supplied header classification strings into their domain enums, raising stable
/// capability errors for unsupported values.
/// </summary>
internal static class JournalEntryHeaderTypes
{
    public static DocumentType ParseDocumentType(string value) =>
        DocumentTypeExtensions.TryParse(value, out var type)
            ? type
            : throw new BusinessRuleException(
                "Unsupported document type.", JournalEntryErrors.UnsupportedDocumentType);

    public static InsertionType ParseInsertionType(string value) =>
        InsertionTypeExtensions.TryParse(value, out var type)
            ? type
            : throw new BusinessRuleException(
                "Unsupported insertion type.", JournalEntryErrors.UnsupportedInsertionType);

    public static BalanceEffect ParseBalanceEffect(string value) =>
        BalanceEffectExtensions.TryParse(value, out var effect)
            ? effect
            : throw new BusinessRuleException(
                "Unsupported balance effect.", JournalEntryErrors.UnsupportedBalanceEffect);
}
