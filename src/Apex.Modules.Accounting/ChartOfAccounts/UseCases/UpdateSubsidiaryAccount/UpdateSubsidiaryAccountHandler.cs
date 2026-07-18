using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateSubsidiaryAccount;

internal sealed class UpdateSubsidiaryAccountHandler(
    IGeneralTransactionRunner tx, ISubsidiaryAccountWriteRepository repo, IClock clock,
    IValidator<UpdateSubsidiaryAccountRequest> validator)
{
    public async Task<UpdateSubsidiaryAccountResponse> HandleAsync(
        long id, UpdateSubsidiaryAccountRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        UpdateSubsidiaryAccountResponse? result = null;
        await tx.ExecuteAsync(async token =>
        {
            var value = await repo.GetForUpdateAsync(id, token)
                ?? throw new NotFoundException("Subsidiary account was not found.", ChartOfAccountsErrors.SubsidiaryAccountNotFound);
            value.Rename(request.Name, clock.UtcNow);
            await repo.UpdateAsync(value, token);
            result = new UpdateSubsidiaryAccountResponse(
                value.Id, value.GeneralAccountId, value.Code, value.Name,
                value.Nature.ToDatabaseValue(), value.DetailAccountType.ToDatabaseValue(), value.Status.ToDatabaseValue());
        }, ct);
        return result!;
    }
}
