using FluentValidation;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateGeneralAccount;

internal sealed class CreateGeneralAccountValidator : AbstractValidator<CreateGeneralAccountRequest>
{
    public CreateGeneralAccountValidator()
    {
        RuleFor(x => x.AccountClassId).GreaterThan(0);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(2);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Nature).IsInEnum();
    }
}
