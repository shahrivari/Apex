using FluentValidation;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateAccountClass;

internal sealed class CreateAccountClassValidator : AbstractValidator<CreateAccountClassRequest>
{
    public CreateAccountClassValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
    }
}
