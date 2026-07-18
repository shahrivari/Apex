using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateAccountClass;

internal sealed class UpdateAccountClassHandler(
    IGeneralTransactionRunner tx, IAccountClassWriteRepository repo, IClock clock,
    IValidator<UpdateAccountClassRequest> validator)
{
    public async Task<UpdateAccountClassResponse> HandleAsync(
        long id, UpdateAccountClassRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        UpdateAccountClassResponse? result = null;
        await tx.ExecuteAsync(async token =>
        {
            var value = await repo.GetForUpdateAsync(id, token)
                ?? throw new NotFoundException("Account class was not found.", ChartOfAccountsErrors.AccountClassNotFound);
            value.Rename(request.Name, clock.UtcNow);
            await repo.UpdateAsync(value, token);
            result = new UpdateAccountClassResponse(value.Id, value.Code, value.Name, value.Status.ToDatabaseValue());
        }, ct);
        return result!;
    }
}
