using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.ArchiveGeneralAccount;

internal sealed class ArchiveGeneralAccountHandler(
    IGeneralTransactionRunner tx, IGeneralAccountWriteRepository repo, IClock clock)
{
    public Task HandleAsync(long id, CancellationToken ct) =>
        tx.ExecuteAsync(async token =>
        {
            var value = await repo.GetForUpdateAsync(id, token)
                ?? throw new NotFoundException("General account was not found.", ChartOfAccountsErrors.GeneralAccountNotFound);
            if (await repo.HasActiveChildrenAsync(id, token))
                throw new BusinessRuleException("General account has active subsidiary accounts.", ChartOfAccountsErrors.HasActiveChildren);
            value.Archive(clock.UtcNow);
            await repo.UpdateAsync(value, token);
        }, ct);
}
