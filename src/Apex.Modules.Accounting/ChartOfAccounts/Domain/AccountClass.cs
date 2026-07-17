using Apex.Application.Abstractions.Exceptions;

namespace Apex.Modules.Accounting.ChartOfAccounts.Domain;

public sealed class AccountClass
{
    public long Id { get; private init; }
    public string Code { get; private init; } = null!;
    public string Name { get; private set; } = null!;
    public AccountStatus Status { get; private set; }
    public DateTime CreatedAt { get; private init; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? ArchivedAt { get; private set; }
    private AccountClass() { }

    public static AccountClass Create(long id, string code, string name, DateTime now)
    {
        Validate(code, name);
        return new() { Id = id, Code = code.Trim().ToUpperInvariant(), Name = name.Trim(), Status = AccountStatus.Active, CreatedAt = now };
    }

    internal static AccountClass Rehydrate(long id, string code, string name, AccountStatus status, DateTime createdAt, DateTime? updatedAt, DateTime? archivedAt) =>
        new() { Id = id, Code = code, Name = name, Status = status, CreatedAt = createdAt, UpdatedAt = updatedAt, ArchivedAt = archivedAt };
    public void Rename(string name, DateTime now) { ValidateName(name); Name = name.Trim(); UpdatedAt = now; }
    public void Archive(DateTime now) { if (Status == AccountStatus.Archived) throw Rule("Account class is already archived.", ChartOfAccountsErrors.AlreadyArchived); Status = AccountStatus.Archived; UpdatedAt = now; ArchivedAt = now; }
    public void Reactivate(DateTime now) { if (Status == AccountStatus.Active) throw Rule("Account class is already active.", ChartOfAccountsErrors.AlreadyActive); Status = AccountStatus.Active; UpdatedAt = now; ArchivedAt = null; }
    private static void Validate(string code, string name) { if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 64) throw Rule("Account code must contain at most 64 characters.", "invalid_account_code"); ValidateName(name); }
    private static void ValidateName(string name) { if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 255) throw Rule("Account name must contain at most 255 characters.", "invalid_account_name"); }
    private static BusinessRuleException Rule(string message, string code) => new(message, code);
}
