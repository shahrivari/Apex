using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.UpdateDraftJournalEntry;

public sealed class UpdateDraftJournalEntryValidator : AbstractValidator<UpdateDraftJournalEntryRequest>
{
    public UpdateDraftJournalEntryValidator()
    {
        RuleFor(x => x.AccountingDate).NotEqual(DateOnly.MinValue);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.DocumentType).NotEmpty();
        RuleFor(x => x.BalanceEffect).NotEmpty();
    }
}
