using Apex.Modules.Accounting.DetailAccounts.Domain;
using Apex.Modules.Accounting.DetailAccounts.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.ListDetailAccounts;

public sealed class ListDetailAccountsHandler(
    IDetailAccountReadRepository repo,
    IValidator<ListDetailAccountsRequest> validator
)
{
    public async Task<ListDetailAccountsResponse> HandleAsync(
        ListDetailAccountsRequest r,
        CancellationToken ct
    )
    {
        await validator.ValidateAndThrowAsync(r, ct);
        var type = r.Type is null ? null : DetailAccountValues.ParseType(r.Type).ToDatabaseValue();
        var status = r.Status is null
            ? null
            : DetailAccountValues.ParseStatus(r.Status).ToDatabaseValue();
        var x = await repo.ListAsync(type, status, r.Search, r.Page, r.PageSize, ct);
        return new(
            x.Items.Select(v => new DetailAccountItem(
                    v.Id,
                    v.Code,
                    v.Name,
                    v.Type,
                    v.Status,
                    v.CreatedAt,
                    v.UpdatedAt,
                    v.ArchivedAt
                ))
                .ToList(),
            x.TotalCount,
            r.Page,
            r.PageSize
        );
    }
}
