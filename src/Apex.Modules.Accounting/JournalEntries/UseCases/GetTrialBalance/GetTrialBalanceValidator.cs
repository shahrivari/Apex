using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetTrialBalance;

public sealed class GetTrialBalanceValidator : AbstractValidator<GetTrialBalanceRequest>
{
    public GetTrialBalanceValidator()
    {
        RuleFor(request => request.AccountingBookId).GreaterThan(0);
        RuleFor(request => request.FiscalYearId).GreaterThan(0);
        RuleFor(request => request.FromDate).NotEmpty();
        RuleFor(request => request.ToDate).GreaterThanOrEqualTo(request => request.FromDate);
        RuleForEach(request => request.ExcludedDocumentTypes)
            .Must(value => Apex.Modules.Accounting.JournalEntries.Domain.DocumentTypeExtensions.TryParse(value, out _));
    }
}
