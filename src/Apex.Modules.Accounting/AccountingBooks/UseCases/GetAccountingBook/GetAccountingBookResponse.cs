namespace Apex.Modules.Accounting.AccountingBooks.UseCases.GetAccountingBook;

public sealed class GetAccountingBookResponse
{
    public long Id { get; init; }

    public string Code { get; init; } = null!;

    public string Title { get; init; } = null!;

    public string OwnerType { get; init; } = null!;

    public string OwnerId { get; init; } = null!;

    public string Status { get; init; } = null!;

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public DateTime? ActivatedAt { get; init; }

    public DateTime? SuspendedAt { get; init; }

    public DateTime? ArchivedAt { get; init; }

    public GetAccountingBookResponse(
        long id,
        string code,
        string title,
        string ownerType,
        string ownerId,
        string status,
        DateTime createdAt,
        DateTime? updatedAt,
        DateTime? activatedAt,
        DateTime? suspendedAt,
        DateTime? archivedAt)
    {
        Id = id;
        Code = code;
        Title = title;
        OwnerType = ownerType;
        OwnerId = ownerId;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ActivatedAt = activatedAt;
        SuspendedAt = suspendedAt;
        ArchivedAt = archivedAt;
    }
}
