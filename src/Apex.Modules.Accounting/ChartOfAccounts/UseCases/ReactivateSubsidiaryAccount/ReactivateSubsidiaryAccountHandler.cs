using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.ReactivateSubsidiaryAccount;

internal sealed class ReactivateSubsidiaryAccountHandler(
    IGeneralTransactionRunner tx, IAccountClassWriteRepository classes, IGeneralAccountWriteRepository parents,
    ISubsidiaryAccountWriteRepository repo, IClock clock)
{
    public Task HandleAsync(long id, CancellationToken ct) =>
        tx.ExecuteAsync(async token =>
        {
            var value = await repo.GetForUpdateAsync(id, token)
                ?? throw new NotFoundException("Subsidiary account was not found.", ChartOfAccountsErrors.SubsidiaryAccountNotFound);
            var parent = await parents.GetForUpdateAsync(value.GeneralAccountId, token)
                ?? throw new NotFoundException("General account was not found.", ChartOfAccountsErrors.GeneralAccountNotFound);
            var root = await classes.GetForUpdateAsync(parent.AccountClassId, token)
                ?? throw new NotFoundException("Account class was not found.", ChartOfAccountsErrors.AccountClassNotFound);
            if (parent.Status != AccountStatus.Active || root.Status != AccountStatus.Active)
                throw new BusinessRuleException("An ancestor account is archived.", ChartOfAccountsErrors.ParentInactive);
            value.Reactivate(clock.UtcNow);
            await repo.UpdateAsync(value, token);
        }, ct);
}
