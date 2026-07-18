using FluentValidation;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateSubsidiaryAccount;

internal sealed class CreateSubsidiaryAccountValidator : AbstractValidator<CreateSubsidiaryAccountRequest>
{
    public CreateSubsidiaryAccountValidator()
    {
        RuleFor(x => x.GeneralAccountId).GreaterThan(0);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(2);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Nature).IsInEnum();
        RuleFor(x => x.DetailAccountType).IsInEnum();
    }
}
