using Apex.Modules.Accounting.JournalEntries.Domain;
using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetCrossFiscalYearTurnover;

public sealed class GetCrossFiscalYearTurnoverValidator : AbstractValidator<GetCrossFiscalYearTurnoverRequest>
{
    public GetCrossFiscalYearTurnoverValidator()
    {
        RuleFor(request => request.AccountingBookId).GreaterThan(0);
        RuleFor(request => request.FiscalYearIds).NotEmpty().Must(ids => ids.Count == ids.Distinct().Count());
        RuleForEach(request => request.FiscalYearIds).GreaterThan(0);
        RuleFor(request => request.FromDate).NotEmpty();
        RuleFor(request => request.ToDate).GreaterThanOrEqualTo(request => request.FromDate);
        RuleForEach(request => request.ExcludedDocumentTypes)
            .Must(value => DocumentTypeExtensions.TryParse(value, out _));
    }
}
