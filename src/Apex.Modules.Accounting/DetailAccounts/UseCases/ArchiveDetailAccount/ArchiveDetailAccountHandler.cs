using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.DetailAccounts.Domain;
using Apex.Modules.Accounting.DetailAccounts.Repositories;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.ArchiveDetailAccount;

public sealed class ArchiveDetailAccountHandler(
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
                x.Archive(clock.UtcNow);
                await repo.UpdateAsync(x, token);
            },
            ct
        );
}
