using FluentValidation;
using Apex.Modules.Accounting.JournalEntries.Domain;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.SearchJournalEntries;

public sealed class SearchJournalEntriesValidator : AbstractValidator<SearchJournalEntriesRequest>
{
    public SearchJournalEntriesValidator()
    {
        RuleFor(x => x.FiscalYearId).GreaterThan(0);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
        RuleFor(x => x.Status)
            .Must(value => JournalEntryStatusExtensions.TryParse(value, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Unsupported journal entry status.");
        RuleFor(x => x.BalanceEffect)
            .Must(value => BalanceEffectExtensions.TryParse(value, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.BalanceEffect))
            .WithMessage("Unsupported balance effect.");
        RuleFor(x => x.DocumentType)
            .Must(value => DocumentTypeExtensions.TryParse(value, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.DocumentType))
            .WithMessage("Unsupported document type.");
        RuleFor(x => x.InsertionType)
            .Must(value => InsertionTypeExtensions.TryParse(value, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.InsertionType))
            .WithMessage("Unsupported insertion type.");
        RuleFor(x => x.SourceType).MaximumLength(64);
        RuleFor(x => x.SourceReference).MaximumLength(200);
    }
}
