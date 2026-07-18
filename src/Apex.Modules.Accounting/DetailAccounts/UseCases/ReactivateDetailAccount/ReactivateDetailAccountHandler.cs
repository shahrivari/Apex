using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.DetailAccounts.Domain;
using Apex.Modules.Accounting.DetailAccounts.Repositories;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.ReactivateDetailAccount;

public sealed class ReactivateDetailAccountHandler(
    IGeneralTransactionRunner tx,
    IDetailAccountWriteRepository repo,
    IClock clock
)
{
    public Task HandleAsync(long id, CancellationToken ct) =>
        tx.ExecuteAsync(
            async token =>
            {
                var x =
                    await repo.GetForUpdateAsync(id, token)
                    ?? throw new NotFoundException(
                        "Detail account was not found.",
                        DetailAccountErrors.NotFound
                    );
                x.Reactivate(clock.UtcNow);
                await repo.UpdateAsync(x, token);
            },
            ct
        );
}
