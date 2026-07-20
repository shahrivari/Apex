using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetTransactionReport;

public sealed class GetTransactionReportValidator : AbstractValidator<GetTransactionReportRequest>
{
    public GetTransactionReportValidator()
    {
        RuleFor(request => request.AccountingBookId).GreaterThan(0);
        RuleFor(request => request.FiscalYearId).GreaterThan(0);
        RuleFor(request => request.ToDate)
            .GreaterThanOrEqualTo(request => request.FromDate!.Value)
            .When(request => request.FromDate.HasValue && request.ToDate.HasValue);
        RuleFor(request => request.Page).GreaterThan(0);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 500);
    }
}
