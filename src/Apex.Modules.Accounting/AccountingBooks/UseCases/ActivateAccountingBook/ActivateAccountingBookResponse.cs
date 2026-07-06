namespace Apex.Modules.Accounting.AccountingBooks.UseCases.ActivateAccountingBook;

public sealed class ActivateAccountingBookResponse
{
    public long Id { get; init; }

    public string Code { get; init; } = null!;

    public string Status { get; init; } = null!;

    public DateTime? ActivatedAt { get; init; }

    public ActivateAccountingBookResponse(long id, string code, string status, DateTime? activatedAt)
    {
        Id = id;
        Code = code;
        Status = status;
        ActivatedAt = activatedAt;
    }
}
