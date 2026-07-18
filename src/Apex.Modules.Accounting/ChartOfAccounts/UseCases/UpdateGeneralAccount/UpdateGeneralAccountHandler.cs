using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateGeneralAccount;

internal sealed class UpdateGeneralAccountHandler(
    IGeneralTransactionRunner tx, IGeneralAccountWriteRepository repo, IClock clock,
    IValidator<UpdateGeneralAccountRequest> validator)
{
    public async Task<UpdateGeneralAccountResponse> HandleAsync(
        long id, UpdateGeneralAccountRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        UpdateGeneralAccountResponse? result = null;
        await tx.ExecuteAsync(async token =>
        {
            var value = await repo.GetForUpdateAsync(id, token)
                ?? throw new NotFoundException("General account was not found.", ChartOfAccountsErrors.GeneralAccountNotFound);
            value.Rename(request.Name, clock.UtcNow);
            await repo.UpdateAsync(value, token);
            result = new UpdateGeneralAccountResponse(
                value.Id, value.AccountClassId, value.Code, value.Name,
                value.Nature.ToDatabaseValue(), value.Status.ToDatabaseValue());
        }, ct);
        return result!;
    }
}
