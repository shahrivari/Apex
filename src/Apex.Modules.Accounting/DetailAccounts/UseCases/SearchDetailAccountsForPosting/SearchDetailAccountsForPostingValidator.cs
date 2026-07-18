using Apex.Modules.Accounting.DetailAccounts.Domain;
using FluentValidation;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.SearchDetailAccountsForPosting;

public sealed class SearchDetailAccountsForPostingValidator
    : AbstractValidator<SearchDetailAccountsForPostingRequest>
{
    public SearchDetailAccountsForPostingValidator()
    {
        RuleFor(x => x.Type)
            .Must(x => Enum.TryParse<DetailAccountType>(x, true, out _))
            .WithErrorCode(DetailAccountErrors.TypeNotSupported);
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
    }
}
