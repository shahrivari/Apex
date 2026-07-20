using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.AppendDraftLines;

public sealed class AppendDraftLinesValidator : AbstractValidator<AppendDraftLinesRequest>
{
    public AppendDraftLinesValidator()
    {
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).SetValidator(new JournalEntryLineRequestValidator());
    }
}
