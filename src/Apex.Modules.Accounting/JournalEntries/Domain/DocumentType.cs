namespace Apex.Modules.Accounting.JournalEntries.Domain;

public enum DocumentType
{
    General,
    Opening,
    Closing,
    TemporaryAccountsClosing,
    PerformanceAccountsClosing
}

public static class DocumentTypeExtensions
{
    public static string ToDatabaseValue(this DocumentType type) => type switch
    {
        DocumentType.General => "GENERAL",
        DocumentType.Opening => "OPENING",
        DocumentType.Closing => "CLOSING",
        DocumentType.TemporaryAccountsClosing => "TEMPORARY_ACCOUNTS_CLOSING",
        DocumentType.PerformanceAccountsClosing => "PERFORMANCE_ACCOUNTS_CLOSING",
        _ => throw new InvalidOperationException($"Unknown document type: {type}.")
    };

    public static DocumentType FromDatabaseValue(string value) => value switch
    {
        "GENERAL" => DocumentType.General,
        "OPENING" => DocumentType.Opening,
        "CLOSING" => DocumentType.Closing,
        "TEMPORARY_ACCOUNTS_CLOSING" => DocumentType.TemporaryAccountsClosing,
        "PERFORMANCE_ACCOUNTS_CLOSING" => DocumentType.PerformanceAccountsClosing,
        _ => throw new InvalidOperationException($"Unknown document type: {value}.")
    };

    public static bool TryParse(string? value, out DocumentType type)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "GENERAL":
                type = DocumentType.General;
                return true;
            case "OPENING":
                type = DocumentType.Opening;
                return true;
            case "CLOSING":
                type = DocumentType.Closing;
                return true;
            case "TEMPORARY_ACCOUNTS_CLOSING":
                type = DocumentType.TemporaryAccountsClosing;
                return true;
            case "PERFORMANCE_ACCOUNTS_CLOSING":
                type = DocumentType.PerformanceAccountsClosing;
                return true;
            default:
                type = default;
                return false;
        }
    }
}
