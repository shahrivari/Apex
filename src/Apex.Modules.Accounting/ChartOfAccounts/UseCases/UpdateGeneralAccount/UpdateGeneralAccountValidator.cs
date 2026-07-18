using FluentValidation;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateGeneralAccount;

internal sealed class UpdateGeneralAccountValidator : AbstractValidator<UpdateGeneralAccountRequest>
{
    public UpdateGeneralAccountValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
}
