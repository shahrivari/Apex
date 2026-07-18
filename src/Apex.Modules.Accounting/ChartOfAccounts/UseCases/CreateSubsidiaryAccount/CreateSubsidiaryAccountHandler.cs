using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Ids;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateSubsidiaryAccount;

internal sealed class CreateSubsidiaryAccountHandler(
    IGeneralTransactionRunner tx, IAccountClassWriteRepository classes, IGeneralAccountWriteRepository parents,
    ISubsidiaryAccountWriteRepository repo, IIdGenerator ids, IClock clock,
    IValidator<CreateSubsidiaryAccountRequest> validator)
{
    public async Task<CreateSubsidiaryAccountResponse> HandleAsync(
        CreateSubsidiaryAccountRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        CreateSubsidiaryAccountResponse? result = null;
        await tx.ExecuteAsync(async token =>
        {
            var parent = await parents.GetForUpdateAsync(request.GeneralAccountId, token)
                ?? throw new NotFoundException("General account was not found.", ChartOfAccountsErrors.GeneralAccountNotFound);
            var root = await classes.GetForUpdateAsync(parent.AccountClassId, token)
                ?? throw new NotFoundException("Account class was not found.", ChartOfAccountsErrors.AccountClassNotFound);
            if (parent.Status != AccountStatus.Active || root.Status != AccountStatus.Active)
                throw new BusinessRuleException("An ancestor account is archived.", ChartOfAccountsErrors.ParentInactive);
            var code = request.Code.Trim().ToUpperInvariant();
            if (await repo.CodeExistsAsync(parent.Id, code, ct: token))
                throw new ConflictException("Subsidiary account code already exists under the general account.", ChartOfAccountsErrors.CodeAlreadyExists);
            var value = SubsidiaryAccount.Create(
                ids.NewId(), parent.Id, code, request.Name, request.Nature, request.DetailAccountType, clock.UtcNow);
            await repo.InsertAsync(value, token);
            result = new CreateSubsidiaryAccountResponse(
                value.Id, value.GeneralAccountId, value.Code, value.Name,
                value.Nature.ToDatabaseValue(), value.DetailAccountType.ToDatabaseValue(), value.Status.ToDatabaseValue());
        }, ct);
        return result!;
    }
}
