namespace Apex.Modules.Accounting.JournalEntries.Domain;

public enum BalanceEffect
{
    Financial,
    Statistical
}

public static class BalanceEffectExtensions
{
    public static string ToDatabaseValue(this BalanceEffect effect) => effect switch
    {
        BalanceEffect.Financial => "FINANCIAL",
        BalanceEffect.Statistical => "STATISTICAL",
        _ => throw new InvalidOperationException($"Unknown balance effect: {effect}.")
    };

    public static BalanceEffect FromDatabaseValue(string value) => value switch
    {
        "FINANCIAL" => BalanceEffect.Financial,
        "STATISTICAL" => BalanceEffect.Statistical,
        _ => throw new InvalidOperationException($"Unknown balance effect: {value}.")
    };

    public static bool TryParse(string? value, out BalanceEffect effect)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "FINANCIAL":
                effect = BalanceEffect.Financial;
                return true;
            case "STATISTICAL":
                effect = BalanceEffect.Statistical;
                return true;
            default:
                effect = default;
                return false;
        }
    }
}
