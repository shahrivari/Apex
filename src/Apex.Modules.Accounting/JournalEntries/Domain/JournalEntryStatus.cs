namespace Apex.Modules.Accounting.JournalEntries.Domain;

public enum JournalEntryStatus
{
    Draft,
    Posted
}

public static class JournalEntryStatusExtensions
{
    public static string ToDatabaseValue(this JournalEntryStatus status) => status switch
    {
        JournalEntryStatus.Draft => "DRAFT",
        JournalEntryStatus.Posted => "POSTED",
        _ => throw new InvalidOperationException($"Unknown journal entry status: {status}.")
    };

    public static JournalEntryStatus FromDatabaseValue(string value) => value switch
    {
        "DRAFT" => JournalEntryStatus.Draft,
        "POSTED" => JournalEntryStatus.Posted,
        _ => throw new InvalidOperationException($"Unknown journal entry status: {value}.")
    };

    public static bool TryParse(string? value, out JournalEntryStatus status)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "DRAFT":
                status = JournalEntryStatus.Draft;
                return true;
            case "POSTED":
                status = JournalEntryStatus.Posted;
                return true;
            default:
                status = default;
                return false;
        }
    }
}
