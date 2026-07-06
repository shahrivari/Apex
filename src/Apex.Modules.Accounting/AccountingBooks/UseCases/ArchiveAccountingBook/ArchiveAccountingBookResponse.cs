namespace Apex.Modules.Accounting.AccountingBooks.UseCases.ArchiveAccountingBook;

public sealed class ArchiveAccountingBookResponse
{
    public long Id { get; init; }

    public string Code { get; init; } = null!;

    public string Status { get; init; } = null!;

    public DateTime? ArchivedAt { get; init; }

    public ArchiveAccountingBookResponse(long id, string code, string status, DateTime? archivedAt)
    {
        Id = id;
        Code = code;
        Status = status;
        ArchivedAt = archivedAt;
    }
}
