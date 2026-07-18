using Apex.Application.Abstractions.Exceptions;

namespace Apex.Modules.Accounting.DetailAccounts.Domain;

public sealed class DetailAccount
{
    public long Id { get; private init; }
    public string Code { get; private init; } = null!;
    public string Name { get; private set; } = null!;
    public DetailAccountType Type { get; private set; }
    public DetailAccountStatus Status { get; private set; }
    public DateTime CreatedAt { get; private init; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? ArchivedAt { get; private set; }

    private DetailAccount() { }

    public static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new BusinessRuleException(
                "Detail account code is required.",
                DetailAccountErrors.InvalidCode
            );
        return code.Trim().ToUpperInvariant();
    }

    public static DetailAccount Create(
        long id,
        string code,
        string name,
        DetailAccountType type,
        DateTime now
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException(
                "Detail account name is required.",
                DetailAccountErrors.InvalidName
            );
        return new DetailAccount
        {
            Id = id,
            Code = NormalizeCode(code),
            Name = name.Trim(),
            Type = type,
            Status = DetailAccountStatus.Active,
            CreatedAt = now,
        };
    }

    internal static DetailAccount Rehydrate(
        long id,
        string code,
        string name,
        DetailAccountType type,
        DetailAccountStatus status,
        DateTime createdAt,
        DateTime? updatedAt,
        DateTime? archivedAt
    ) =>
        new()
        {
            Id = id,
            Code = code,
            Name = name,
            Type = type,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            ArchivedAt = archivedAt,
        };

    public void Update(string name, DetailAccountType type, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException(
                "Detail account name is required.",
                DetailAccountErrors.InvalidName
            );
        Name = name.Trim();
        Type = type;
        UpdatedAt = now;
    }

    public void Archive(DateTime now)
    {
        if (Status == DetailAccountStatus.Archived)
            throw new BusinessRuleException(
                "Detail account is already archived.",
                DetailAccountErrors.AlreadyArchived
            );
        Status = DetailAccountStatus.Archived;
        UpdatedAt = now;
        ArchivedAt = now;
    }

    public void Reactivate(DateTime now)
    {
        if (Status == DetailAccountStatus.Active)
            throw new BusinessRuleException(
                "Detail account is already active.",
                DetailAccountErrors.AlreadyActive
            );
        Status = DetailAccountStatus.Active;
        UpdatedAt = now;
        ArchivedAt = null;
    }
}
