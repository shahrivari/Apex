using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetJournalEntryAudit;

public sealed class GetJournalEntryAuditValidator : AbstractValidator<GetJournalEntryAuditRequest>
{
    public GetJournalEntryAuditValidator()
    {
        RuleFor(request => request.AccountingBookId).GreaterThan(0);
        RuleFor(request => request.FiscalYearId).GreaterThan(0);
        RuleFor(request => request.ReferenceNumber).GreaterThan(0);
    }
}
