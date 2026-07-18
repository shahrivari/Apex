using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Ids;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.DetailAccounts.Domain;
using Apex.Modules.Accounting.DetailAccounts.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.CreateDetailAccount;

public sealed class CreateDetailAccountHandler(
    IGeneralTransactionRunner tx,
    IDetailAccountWriteRepository repo,
    IIdGenerator ids,
    IClock clock,
    IValidator<CreateDetailAccountRequest> validator
)
{
    public async Task<CreateDetailAccountResponse> HandleAsync(
        CreateDetailAccountRequest request,
        CancellationToken ct
    )
    {
        await validator.ValidateAndThrowAsync(request, ct);
        CreateDetailAccountResponse? result = null;
        await tx.ExecuteAsync(
            async token =>
            {
                var code = DetailAccount.NormalizeCode(request.Code);
                if (await repo.CodeExistsAsync(code, token))
                    throw new ConflictException(
                        "Detail account code already exists.",
                        DetailAccountErrors.CodeAlreadyExists
                    );
                var value = DetailAccount.Create(
                    ids.NewId(),
                    code,
                    request.Name,
                    DetailAccountValues.ParseType(request.Type),
                    clock.UtcNow
                );
                await repo.InsertAsync(value, token);
                result = new(
                    value.Id,
                    value.Code,
                    value.Name,
                    value.Type.ToDatabaseValue(),
                    value.Status.ToDatabaseValue(),
                    value.CreatedAt
                );
            },
            ct
        );
        return result!;
    }
}
