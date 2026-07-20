using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases;

public sealed class JournalEntryLineRequestValidator : AbstractValidator<JournalEntryLineRequest>
{
    public JournalEntryLineRequestValidator()
    {
        RuleFor(x => x.Side).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.AccountClassCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.GeneralAccountCode).NotEmpty().MaximumLength(2);
        RuleFor(x => x.SubsidiaryAccountCode).NotEmpty().MaximumLength(2);
        RuleFor(x => x.DetailAccountCode).MaximumLength(50);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.RowNumber).GreaterThan(0).When(x => x.RowNumber.HasValue);
    }
}
