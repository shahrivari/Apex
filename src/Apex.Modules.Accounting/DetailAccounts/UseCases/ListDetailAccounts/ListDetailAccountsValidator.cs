using Apex.Modules.Accounting.DetailAccounts.Domain;
using FluentValidation;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.ListDetailAccounts;

public sealed class ListDetailAccountsValidator : AbstractValidator<ListDetailAccountsRequest>
{
    public ListDetailAccountsValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Type)
            .Must(x => x is null || Enum.TryParse<DetailAccountType>(x, true, out _));
        RuleFor(x => x.Status)
            .Must(x => x is null || Enum.TryParse<DetailAccountStatus>(x, true, out _));
    }
}
