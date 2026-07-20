using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.ReplaceDraftLines;

public sealed class ReplaceDraftLinesValidator : AbstractValidator<ReplaceDraftLinesRequest>
{
    public ReplaceDraftLinesValidator()
    {
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).SetValidator(new JournalEntryLineRequestValidator());
    }
}
