using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Ids;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateAccountClass;

internal sealed class CreateAccountClassHandler(
    IGeneralTransactionRunner tx, IAccountClassWriteRepository repo, IIdGenerator ids, IClock clock,
    IValidator<CreateAccountClassRequest> validator)
{
    public async Task<CreateAccountClassResponse> HandleAsync(
        CreateAccountClassRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        CreateAccountClassResponse? result = null;
        await tx.ExecuteAsync(async token =>
        {
            var code = request.Code.Trim().ToUpperInvariant();
            if (await repo.CodeExistsAsync(code, ct: token))
                throw new ConflictException("Account class code already exists.", ChartOfAccountsErrors.CodeAlreadyExists);
            var value = AccountClass.Create(ids.NewId(), code, request.Name, clock.UtcNow);
            await repo.InsertAsync(value, token);
            result = new CreateAccountClassResponse(value.Id, value.Code, value.Name, value.Status.ToDatabaseValue());
        }, ct);
        return result!;
    }
}
