using FluentValidation;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateAccountClass;

internal sealed class UpdateAccountClassValidator : AbstractValidator<UpdateAccountClassRequest>
{
    public UpdateAccountClassValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
}
