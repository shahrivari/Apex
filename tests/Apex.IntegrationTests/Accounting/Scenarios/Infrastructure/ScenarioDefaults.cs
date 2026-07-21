namespace Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;

/// <summary>
/// Fixed, business-readable constants shared by Accounting scenario tests: a canonical fiscal
/// year window and the real accepted string literals for Journal Entry header/line
/// classification (confirmed against <c>DocumentType</c>, <c>InsertionType</c>,
/// <c>BalanceEffect</c>, and <c>JournalEntrySide</c> in
/// <c>Apex.Modules.Accounting.JournalEntries.Domain</c> — never invented values).
/// </summary>
public static class ScenarioDefaults
{
    /// <summary>Canonical fiscal year window used by scenarios that do not need a different range.</summary>
    public static readonly DateOnly FiscalYearStart = new(2026, 1, 1);
    public static readonly DateOnly FiscalYearEnd = new(2026, 12, 31);

    // Journal Entry document types (Apex.Modules.Accounting.JournalEntries.Domain.DocumentType).
    public const string DocumentTypeGeneral = "GENERAL";
    public const string DocumentTypeOpening = "OPENING";
    public const string DocumentTypeClosing = "CLOSING";
    public const string DocumentTypeTemporaryAccountsClosing = "TEMPORARY_ACCOUNTS_CLOSING";
    public const string DocumentTypePerformanceAccountsClosing = "PERFORMANCE_ACCOUNTS_CLOSING";

    // Journal Entry insertion types (Apex.Modules.Accounting.JournalEntries.Domain.InsertionType).
    public const string InsertionTypeManual = "MANUAL";
    public const string InsertionTypeSemiSystem = "SEMI_SYSTEM";
    public const string InsertionTypeSystem = "SYSTEM";
    public const string InsertionTypeMigration = "MIGRATION";

    // Journal Entry balance effects (Apex.Modules.Accounting.JournalEntries.Domain.BalanceEffect).
    public const string BalanceEffectFinancial = "FINANCIAL";
    public const string BalanceEffectStatistical = "STATISTICAL";

    // Journal Entry line sides (Apex.Modules.Accounting.JournalEntries.Domain.JournalEntrySide).
    public const string SideDebit = "DEBIT";
    public const string SideCredit = "CREDIT";

    // Detail-account requirement recorded on a Subsidiary Account
    // (Apex.Modules.Accounting.ChartOfAccounts.Domain.DetailAccountType). Every Subsidiary Account
    // requires one of these — a "no detail account" requirement no longer exists.
    public const string DetailRequirementBank = "BANK";
    public const string DetailRequirementSymbol = "SYMBOL";
    public const string DetailRequirementPerson = "PERSON";

    // Concrete Detail Account types (Apex.Modules.Accounting.DetailAccounts.Domain.DetailAccountType).
    public const string DetailAccountTypePerson = "PERSON";
    public const string DetailAccountTypeSymbol = "SYMBOL";
    public const string DetailAccountTypeBank = "BANK";

    // A standard PERSON Detail Account seeded by generic scenarios so every posted line can carry a
    // Detail Account code (now mandatory on every line). Its type matches the PERSON requirement the
    // generic charts give their Subsidiary Accounts, so it is valid on any of their lines.
    public const string StandardDetailCode = "DET-1";
    public const string StandardDetailName = "Standard Detail";

    /// <summary>
    /// Builds a readable, collision-resistant business code. No static counters or shared mutable
    /// state — every call is independent, so parallel or repeated scenario runs never collide even
    /// though the database is also reset between tests. Capped at 48 characters so callers can
    /// safely use it for any of the capability code fields (book/account-class codes allow 64,
    /// detail-account codes allow 50).
    /// </summary>
    public static string UniqueCode(string tag)
    {
        var candidate = $"{tag}-{Guid.NewGuid():N}";
        return candidate.Length <= 48 ? candidate : candidate[..48];
    }
}
