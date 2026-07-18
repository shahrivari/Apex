using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.DetailAccounts.Domain;
using Apex.Modules.Accounting.DetailAccounts.Repositories;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.GetDetailAccountByCode;

public sealed class GetDetailAccountByCodeHandler(IDetailAccountReadRepository repo)
{
    public async Task<GetDetailAccountByCodeResponse> HandleAsync(string code, CancellationToken ct)
    {
        var x =
            await repo.GetByCodeAsync(DetailAccount.NormalizeCode(code), ct)
            ?? throw new NotFoundException(
                "Detail account was not found.",
                DetailAccountErrors.NotFound
            );
        return new(x.Id, x.Code, x.Name, x.Type, x.Status, x.CreatedAt, x.UpdatedAt, x.ArchivedAt);
    }
}
