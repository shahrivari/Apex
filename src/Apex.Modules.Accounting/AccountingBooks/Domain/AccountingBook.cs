using Apex.Application.Abstractions.Exceptions;

namespace Apex.Modules.Accounting.AccountingBooks.Domain;

public sealed class AccountingBook
{
    public long Id { get; init; }

    public string Code { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    public string OwnerType { get; private set; } = null!;

    public string OwnerId { get; private set; } = null!;

    public AccountingBookStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public DateTime? ActivatedAt { get; private set; }

    public DateTime? SuspendedAt { get; private set; }

    public DateTime? ArchivedAt { get; private set; }

    internal AccountingBook()
    {
    }

    internal static AccountingBook CreateFromSql(
        long id,
        string code,
        string title,
        string ownerType,
        string ownerId,
        AccountingBookStatus status,
        DateTime createdAt,
        DateTime? updatedAt,
        DateTime? activatedAt,
        DateTime? suspendedAt,
        DateTime? archivedAt)
    {
        return new AccountingBook
        {
            Id = id,
            Code = code,
            Title = title,
            OwnerType = ownerType,
            OwnerId = ownerId,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            ActivatedAt = activatedAt,
            SuspendedAt = suspendedAt,
            ArchivedAt = archivedAt
        };
    }

    internal static AccountingBook CreateFromSql(SqlModels.AccountingBookSqlModel model)
    {
        return CreateFromSql(
            model.Id,
            model.Code,
            model.Title,
            model.OwnerType,
            model.OwnerId,
            AccountingBookStatusExtensions.FromDatabaseValue(model.Status),
            model.CreatedAt,
            model.UpdatedAt,
            model.ActivatedAt,
            model.SuspendedAt,
            model.ArchivedAt);
    }

    public static AccountingBook Create(
        long id,
        string code,
        string title,
        string ownerType,
        string ownerId,
        DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new BusinessRuleException("Accounting book code is required.", "invalid_accounting_book_code");

        if (string.IsNullOrWhiteSpace(title))
            throw new BusinessRuleException("Accounting book title is required.", "invalid_accounting_book_title");

        if (string.IsNullOrWhiteSpace(ownerType))
            throw new BusinessRuleException("Accounting book owner type is required.", "invalid_accounting_book_owner");

        if (string.IsNullOrWhiteSpace(ownerId))
            throw new BusinessRuleException("Accounting book owner ID is required.", "invalid_accounting_book_owner");

        return new AccountingBook
        {
            Id = id,
            Code = code.Trim().ToUpperInvariant(),
            Title = title.Trim(),
            OwnerType = ownerType.Trim().ToUpperInvariant(),
            OwnerId = ownerId.Trim(),
            Status = AccountingBookStatus.Draft,
            CreatedAt = createdAt
        };
    }

    public void Activate(DateTime now)
    {
        if (Status != AccountingBookStatus.Draft && Status != AccountingBookStatus.Suspended)
        {
            throw new BusinessRuleException(
                $"Accounting book cannot be activated from status {Status.ToDatabaseValue()}.",
                AccountingBookErrors.AccountingBookCannotBeActivated);
        }

        Status = AccountingBookStatus.Active;
        ActivatedAt = now;
        UpdatedAt = now;
    }

    public void Suspend(DateTime now)
    {
        if (Status != AccountingBookStatus.Active)
        {
            throw new BusinessRuleException(
                $"Accounting book cannot be suspended from status {Status.ToDatabaseValue()}.",
                AccountingBookErrors.AccountingBookCannotBeSuspended);
        }

        Status = AccountingBookStatus.Suspended;
        SuspendedAt = now;
        UpdatedAt = now;
    }

    public void Archive(DateTime now)
    {
        if (Status == AccountingBookStatus.Active)
        {
            throw new BusinessRuleException(
                "Accounting book must be suspended before archiving.",
                AccountingBookErrors.AccountingBookCannotBeArchived);
        }

        if (Status == AccountingBookStatus.Archived)
        {
            throw new BusinessRuleException(
                "Accounting book is already archived.",
                AccountingBookErrors.AccountingBookCannotBeArchived);
        }

        Status = AccountingBookStatus.Archived;
        ArchivedAt = now;
        UpdatedAt = now;
    }

    public void Rename(string title, DateTime now)
    {
        if (Status == AccountingBookStatus.Archived)
        {
            throw new BusinessRuleException(
                "Archived accounting book cannot be renamed.",
                "archived_accounting_book_cannot_be_changed");
        }

        if (string.IsNullOrWhiteSpace(title))
            throw new BusinessRuleException("Accounting book title is required.", "invalid_accounting_book_title");

        Title = title.Trim();
        UpdatedAt = now;
    }
}
