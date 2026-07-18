using FluentValidation;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateSubsidiaryAccount;

internal sealed class UpdateSubsidiaryAccountValidator : AbstractValidator<UpdateSubsidiaryAccountRequest>
{
    public UpdateSubsidiaryAccountValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
}
