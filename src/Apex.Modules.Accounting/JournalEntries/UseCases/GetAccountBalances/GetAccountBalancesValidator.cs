using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetAccountBalances;

public sealed class GetAccountBalancesValidator : AbstractValidator<GetAccountBalancesRequest>
{
    public GetAccountBalancesValidator()
    {
        RuleFor(request => request.AccountingBookId).GreaterThan(0);
        RuleFor(request => request.FiscalYearId).GreaterThan(0);
        RuleFor(request => request.AsOfDate).NotEmpty();
    }
}
