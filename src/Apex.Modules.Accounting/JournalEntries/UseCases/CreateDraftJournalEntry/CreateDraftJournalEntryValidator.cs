using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.CreateDraftJournalEntry;

public sealed class CreateDraftJournalEntryValidator : AbstractValidator<CreateDraftJournalEntryRequest>
{
    public CreateDraftJournalEntryValidator()
    {
        RuleFor(x => x.AccountingBookId).GreaterThan(0);
        RuleFor(x => x.FiscalYearId).GreaterThan(0);
        RuleFor(x => x.AccountingDate).NotEqual(DateOnly.MinValue);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.DocumentType).NotEmpty();
        RuleFor(x => x.InsertionType).NotEmpty();
        RuleFor(x => x.BalanceEffect).NotEmpty();
        RuleFor(x => x.SourceType).MaximumLength(64);
        RuleFor(x => x.SourceReference).MaximumLength(200);
        RuleFor(x => x.SourceType)
            .NotEmpty()
            .When(x => !string.IsNullOrWhiteSpace(x.SourceReference));
        RuleFor(x => x.SourceReference)
            .NotEmpty()
            .When(x => !string.IsNullOrWhiteSpace(x.SourceType));
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).SetValidator(new JournalEntryLineRequestValidator());
    }
}
