using Apex.Modules.Accounting.AccountingBooks.Domain;

namespace Apex.Modules.Accounting.AccountingBooks.SqlModels;

public sealed class AccountingBookSqlModel
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string OwnerType { get; set; } = null!;

    public string OwnerId { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public DateTime? SuspendedAt { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public AccountingBook MapToDomain()
    {
        return AccountingBook.CreateFromSql(this);
    }
}
