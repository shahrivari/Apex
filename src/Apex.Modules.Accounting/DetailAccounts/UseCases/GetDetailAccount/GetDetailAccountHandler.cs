using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.DetailAccounts.Domain;
using Apex.Modules.Accounting.DetailAccounts.Repositories;
using Apex.Modules.Accounting.DetailAccounts.Repositories.Rows;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.GetDetailAccount;

public sealed class GetDetailAccountHandler(IDetailAccountReadRepository repo)
{
    public async Task<GetDetailAccountResponse> HandleAsync(long id, CancellationToken ct) =>
        Map(
            await repo.GetByIdAsync(id, ct)
                ?? throw new NotFoundException(
                    "Detail account was not found.",
                    DetailAccountErrors.NotFound
                )
        );

    private static GetDetailAccountResponse Map(DetailAccountRow x) =>
        new(x.Id, x.Code, x.Name, x.Type, x.Status, x.CreatedAt, x.UpdatedAt, x.ArchivedAt);
}
