using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Ids;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateGeneralAccount;

internal sealed class CreateGeneralAccountHandler(
    IGeneralTransactionRunner tx, IAccountClassWriteRepository parents, IGeneralAccountWriteRepository repo,
    IIdGenerator ids, IClock clock, IValidator<CreateGeneralAccountRequest> validator)
{
    public async Task<CreateGeneralAccountResponse> HandleAsync(
        CreateGeneralAccountRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        CreateGeneralAccountResponse? result = null;
        await tx.ExecuteAsync(async token =>
        {
            var parent = await parents.GetForUpdateAsync(request.AccountClassId, token)
                ?? throw new NotFoundException("Account class was not found.", ChartOfAccountsErrors.AccountClassNotFound);
            if (parent.Status != AccountStatus.Active)
                throw new BusinessRuleException("Account class is archived.", ChartOfAccountsErrors.ParentInactive);
            var code = request.Code.Trim().ToUpperInvariant();
            if (await repo.CodeExistsAsync(parent.Id, code, ct: token))
                throw new ConflictException("General account code already exists under the account class.", ChartOfAccountsErrors.CodeAlreadyExists);
            var value = GeneralAccount.Create(ids.NewId(), parent.Id, code, request.Name, request.Nature, clock.UtcNow);
            await repo.InsertAsync(value, token);
            result = new CreateGeneralAccountResponse(
                value.Id, value.AccountClassId, value.Code, value.Name,
                value.Nature.ToDatabaseValue(), value.Status.ToDatabaseValue());
        }, ct);
        return result!;
    }
}
