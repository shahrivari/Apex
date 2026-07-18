using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.DetailAccounts.Domain;
using Apex.Modules.Accounting.DetailAccounts.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.UpdateDetailAccount;

public sealed class UpdateDetailAccountHandler(
    IGeneralTransactionRunner tx,
    IDetailAccountWriteRepository repo,
    IClock clock,
    IValidator<UpdateDetailAccountRequest> validator
)
{
    public async Task<UpdateDetailAccountResponse> HandleAsync(
        long id,
        UpdateDetailAccountRequest r,
        CancellationToken ct
    )
    {
        await validator.ValidateAndThrowAsync(r, ct);
        UpdateDetailAccountResponse? result = null;
        await tx.ExecuteAsync(
            async token =>
            {
                var value =
                    await repo.GetForUpdateAsync(id, token)
                    ?? throw new NotFoundException(
                        "Detail account was not found.",
                        DetailAccountErrors.NotFound
                    );
                if (r.Code is not null && DetailAccount.NormalizeCode(r.Code) != value.Code)
                    throw new BusinessRuleException(
                        "Detail account code is immutable.",
                        DetailAccountErrors.CodeImmutable
                    );
                value.Update(r.Name, DetailAccountValues.ParseType(r.Type), clock.UtcNow);
                await repo.UpdateAsync(value, token);
                result = new(
                    value.Id,
                    value.Code,
                    value.Name,
                    value.Type.ToDatabaseValue(),
                    value.Status.ToDatabaseValue(),
                    value.UpdatedAt
                );
            },
            ct
        );
        return result!;
    }
}
