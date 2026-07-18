using Apex.Application.Abstractions.Exceptions;

namespace Apex.Modules.Accounting.ChartOfAccounts.Domain;

public sealed class GeneralAccount
{
    public long Id { get; private init; }
    public long AccountClassId { get; private init; }
    public string Code { get; private init; } = null!;
    public string Name { get; private set; } = null!;
    public AccountNature Nature { get; private init; }
    public AccountStatus Status { get; private set; }
    public DateTime CreatedAt { get; private init; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? ArchivedAt { get; private set; }

    private GeneralAccount() { }

    public static GeneralAccount Create(
        long id, long parentId, string code, string name, AccountNature nature, DateTime now)
    {
        Validate(parentId, code, name);
        return new GeneralAccount
        {
            Id = id,
            AccountClassId = parentId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Nature = nature,
            Status = AccountStatus.Active,
            CreatedAt = now
        };
    }

    internal static GeneralAccount Rehydrate(
        long id, long parentId, string code, string name, AccountNature nature, AccountStatus status,
        DateTime createdAt, DateTime? updatedAt, DateTime? archivedAt) => new()
        {
            Id = id,
            AccountClassId = parentId,
            Code = code,
            Name = name,
            Nature = nature,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            ArchivedAt = archivedAt
        };

    public void Rename(string name, DateTime now)
    {
        ValidateName(name);
        Name = name.Trim();
        UpdatedAt = now;
    }

    public void Archive(DateTime now)
    {
        if (Status == AccountStatus.Archived)
            throw Rule("General account is already archived.", ChartOfAccountsErrors.AlreadyArchived);
        Status = AccountStatus.Archived;
        UpdatedAt = now;
        ArchivedAt = now;
    }

    public void Reactivate(DateTime now)
    {
        if (Status == AccountStatus.Active)
            throw Rule("General account is already active.", ChartOfAccountsErrors.AlreadyActive);
        Status = AccountStatus.Active;
        UpdatedAt = now;
        ArchivedAt = null;
    }

    private static void Validate(long parentId, string code, string name)
    {
        if (parentId <= 0)
            throw Rule("Account class is required.", ChartOfAccountsErrors.AccountClassNotFound);
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 2)
            throw Rule("Title account code must contain at most 2 characters.", ChartOfAccountsErrors.InvalidCode);
        ValidateName(name);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 255)
            throw Rule("Account name must contain at most 255 characters.", ChartOfAccountsErrors.InvalidName);
    }

    private static BusinessRuleException Rule(string message, string code) => new(message, code);
}
