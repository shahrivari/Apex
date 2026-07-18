using FluentValidation;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.SearchAccounts;

internal sealed class SearchAccountsValidator : AbstractValidator<SearchAccountsRequest>
{
    public SearchAccountsValidator()
    {
        RuleFor(x => x.ParentId).GreaterThan(0).When(x => x.ParentId.HasValue);
        RuleFor(x => x.Term).MaximumLength(255);
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Level).IsInEnum().When(x => x.Level.HasValue);
        RuleFor(x => x.Nature).IsInEnum().When(x => x.Nature.HasValue);
        RuleFor(x => x.DetailAccountType).IsInEnum().When(x => x.DetailAccountType.HasValue);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
    }
}
