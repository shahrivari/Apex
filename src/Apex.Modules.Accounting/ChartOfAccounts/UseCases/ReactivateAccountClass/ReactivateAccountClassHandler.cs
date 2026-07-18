using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.ReactivateAccountClass;

internal sealed class ReactivateAccountClassHandler(
    IGeneralTransactionRunner tx, IAccountClassWriteRepository repo, IClock clock)
{
    public Task HandleAsync(long id, CancellationToken ct) =>
        tx.ExecuteAsync(async token =>
        {
            var value = await repo.GetForUpdateAsync(id, token)
                ?? throw new NotFoundException("Account class was not found.", ChartOfAccountsErrors.AccountClassNotFound);
            value.Reactivate(clock.UtcNow);
            await repo.UpdateAsync(value, token);
        }, ct);
}
