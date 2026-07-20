using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.ReverseJournalEntry;

public sealed class ReverseJournalEntryValidator : AbstractValidator<ReverseJournalEntryRequest>
{
    public ReverseJournalEntryValidator()
    {
        RuleFor(request => request.AccountingDate).NotEmpty();
        RuleFor(request => request.ReversalReason).NotEmpty().MaximumLength(1024);
    }
}
