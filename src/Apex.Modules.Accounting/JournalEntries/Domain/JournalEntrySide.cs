namespace Apex.Modules.Accounting.JournalEntries.Domain;

public enum JournalEntrySide
{
    Debit,
    Credit
}

public static class JournalEntrySideExtensions
{
    public static string ToDatabaseValue(this JournalEntrySide side) => side switch
    {
        JournalEntrySide.Debit => "DEBIT",
        JournalEntrySide.Credit => "CREDIT",
        _ => throw new InvalidOperationException($"Unknown journal entry side: {side}.")
    };

    public static JournalEntrySide FromDatabaseValue(string value) => value switch
    {
        "DEBIT" => JournalEntrySide.Debit,
        "CREDIT" => JournalEntrySide.Credit,
        _ => throw new InvalidOperationException($"Unknown journal entry side: {value}.")
    };

    public static bool TryParse(string? value, out JournalEntrySide side)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "DEBIT":
                side = JournalEntrySide.Debit;
                return true;
            case "CREDIT":
                side = JournalEntrySide.Credit;
                return true;
            default:
                side = default;
                return false;
        }
    }
}
