namespace Apex.Modules.Accounting.JournalEntries.Domain;

public enum InsertionType
{
    Manual,
    SemiSystem,
    System,
    Migration
}

public static class InsertionTypeExtensions
{
    public static string ToDatabaseValue(this InsertionType type) => type switch
    {
        InsertionType.Manual => "MANUAL",
        InsertionType.SemiSystem => "SEMI_SYSTEM",
        InsertionType.System => "SYSTEM",
        InsertionType.Migration => "MIGRATION",
        _ => throw new InvalidOperationException($"Unknown insertion type: {type}.")
    };

    public static InsertionType FromDatabaseValue(string value) => value switch
    {
        "MANUAL" => InsertionType.Manual,
        "SEMI_SYSTEM" => InsertionType.SemiSystem,
        "SYSTEM" => InsertionType.System,
        "MIGRATION" => InsertionType.Migration,
        _ => throw new InvalidOperationException($"Unknown insertion type: {value}.")
    };

    public static bool TryParse(string? value, out InsertionType type)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "MANUAL":
                type = InsertionType.Manual;
                return true;
            case "SEMI_SYSTEM":
                type = InsertionType.SemiSystem;
                return true;
            case "SYSTEM":
                type = InsertionType.System;
                return true;
            case "MIGRATION":
                type = InsertionType.Migration;
                return true;
            default:
                type = default;
                return false;
        }
    }
}
