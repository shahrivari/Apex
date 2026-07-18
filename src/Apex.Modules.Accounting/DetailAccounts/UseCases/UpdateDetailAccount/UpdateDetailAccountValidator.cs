using Apex.Modules.Accounting.DetailAccounts.Domain;
using FluentValidation;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.UpdateDetailAccount;

public sealed class UpdateDetailAccountValidator : AbstractValidator<UpdateDetailAccountRequest>
{
    public UpdateDetailAccountValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithErrorCode(DetailAccountErrors.InvalidName);
        RuleFor(x => x.Type)
            .Must(x => Enum.TryParse<DetailAccountType>(x, true, out _))
            .WithErrorCode(DetailAccountErrors.TypeNotSupported);
    }
}
