namespace Apex.Modules.Accounting.AccountingBooks.UseCases.SuspendAccountingBook;

public sealed class SuspendAccountingBookResponse
{
    public long Id { get; init; }

    public string Code { get; init; } = null!;

    public string Status { get; init; } = null!;

    public DateTime? SuspendedAt { get; init; }

    public SuspendAccountingBookResponse(long id, string code, string status, DateTime? suspendedAt)
    {
        Id = id;
        Code = code;
        Status = status;
        SuspendedAt = suspendedAt;
    }
}
