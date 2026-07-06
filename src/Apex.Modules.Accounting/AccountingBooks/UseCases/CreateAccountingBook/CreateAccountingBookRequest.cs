namespace Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;

public sealed class CreateAccountingBookRequest
{
    public string Code { get; init; } = null!;

    public string Title { get; init; } = null!;

    public string OwnerType { get; init; } = null!;

    public string OwnerId { get; init; } = null!;
}
