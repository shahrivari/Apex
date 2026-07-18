using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.DetailAccounts.Domain;
using Apex.Modules.Accounting.DetailAccounts.Repositories;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.ValidateDetailAccountForPosting;

public sealed class ValidateDetailAccountForPostingHandler(IDetailAccountReadRepository repo)
    : IDetailAccountPostingValidator
{
    public async Task ValidateAsync(
        string? code,
        string? requiredType,
        CancellationToken ct = default
    )
    {
        if (string.Equals(requiredType, "NONE", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(code))
                throw new BusinessRuleException(
                    "The subsidiary account does not accept a detail account.",
                    DetailAccountErrors.NotAllowed
                );
            return;
        }
        if (string.IsNullOrWhiteSpace(code))
            throw new BusinessRuleException(
                "A detail account is required.",
                DetailAccountErrors.Required
            );
        var type = DetailAccountValues.ParseType(requiredType ?? "");
        var row =
            await repo.GetByCodeAsync(DetailAccount.NormalizeCode(code), ct)
            ?? throw new NotFoundException(
                "Detail account was not found.",
                DetailAccountErrors.NotFound
            );
        if (row.Status != "ACTIVE")
            throw new BusinessRuleException(
                "Detail account is archived.",
                DetailAccountErrors.Archived
            );
        if (row.Type != type.ToDatabaseValue())
            throw new BusinessRuleException(
                "Detail account type does not match.",
                DetailAccountErrors.TypeMismatch
            );
    }
}
