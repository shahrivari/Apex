namespace Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;

public sealed class CreateAccountingBookResponse
{
    public long Id { get; init; }

    public string Code { get; init; } = null!;

    public string Status { get; init; } = null!;

    public CreateAccountingBookResponse(long id, string code, string status)
    {
        Id = id;
        Code = code;
        Status = status;
    }
}
