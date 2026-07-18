using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.ReactivateGeneralAccount;

internal sealed class ReactivateGeneralAccountHandler(
    IGeneralTransactionRunner tx, IAccountClassWriteRepository parents, IGeneralAccountWriteRepository repo,
    IClock clock)
{
    public Task HandleAsync(long id, CancellationToken ct) =>
        tx.ExecuteAsync(async token =>
        {
            var value = await repo.GetForUpdateAsync(id, token)
                ?? throw new NotFoundException("General account was not found.", ChartOfAccountsErrors.GeneralAccountNotFound);
            var parent = await parents.GetForUpdateAsync(value.AccountClassId, token)
                ?? throw new NotFoundException("Account class was not found.", ChartOfAccountsErrors.AccountClassNotFound);
            if (parent.Status != AccountStatus.Active)
                throw new BusinessRuleException("Account class is archived.", ChartOfAccountsErrors.ParentInactive);
            value.Reactivate(clock.UtcNow);
            await repo.UpdateAsync(value, token);
        }, ct);
}
