namespace Apex.Modules.Accounting.AccountingBooks.Repositories.Rows;

public sealed class AccountingBookRow
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
}
