using Apex.Modules.Accounting.DetailAccounts.Domain;
using FluentValidation;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.CreateDetailAccount;

public sealed class CreateDetailAccountValidator : AbstractValidator<CreateDetailAccountRequest>
{
    public CreateDetailAccountValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50)
            .WithErrorCode(DetailAccountErrors.InvalidCode);
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithErrorCode(DetailAccountErrors.InvalidName);
        RuleFor(x => x.Type)
            .Must(x => Enum.TryParse<DetailAccountType>(x, true, out _))
            .WithErrorCode(DetailAccountErrors.TypeNotSupported);
    }
}
