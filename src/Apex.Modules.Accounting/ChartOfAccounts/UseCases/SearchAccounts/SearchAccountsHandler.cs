using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.SearchAccounts;

internal sealed class SearchAccountsHandler(
    IAccountClassReadRepository classes, IGeneralAccountReadRepository generals,
    ISubsidiaryAccountReadRepository subsidiaries, IValidator<SearchAccountsRequest> validator)
{
    public async Task<SearchAccountsResponse> HandleAsync(SearchAccountsRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var includeArchived = request.Status != AccountStatus.Active;

        var all = new List<SearchAccountItem>();
        if (request.Level is null or AccountLevel.AccountClass)
            all.AddRange((await classes.ListAsync(includeArchived, ct))
                .Select(x => new SearchAccountItem(x.Id, "ACCOUNT_CLASS", null, x.Code, x.Name, null, null, x.Status)));
        if (request.Level is null or AccountLevel.GeneralAccount)
            all.AddRange((await generals.ListAsync(includeArchived, ct))
                .Select(x => new SearchAccountItem(x.Id, "GENERAL_ACCOUNT", x.AccountClassId, x.Code, x.Name, x.Nature, null, x.Status)));
        if (request.Level is null or AccountLevel.SubsidiaryAccount)
            all.AddRange((await subsidiaries.ListAsync(includeArchived, ct))
                .Select(x => new SearchAccountItem(x.Id, "SUBSIDIARY_ACCOUNT", x.GeneralAccountId, x.Code, x.Name, x.Nature, x.DetailAccountType, x.Status)));

        IEnumerable<SearchAccountItem> query = all;
        if (request.ParentId.HasValue)
            query = query.Where(x => x.ParentId == request.ParentId);
        if (!string.IsNullOrWhiteSpace(request.Term))
        {
            var term = request.Term.Trim();
            query = query.Where(x => x.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (request.Nature.HasValue)
            query = query.Where(x => x.Nature == request.Nature.Value.ToDatabaseValue());
        if (request.DetailAccountType.HasValue)
            query = query.Where(x => x.DetailAccountType == request.DetailAccountType.Value.ToDatabaseValue());
        if (request.Status.HasValue)
            query = query.Where(x => x.Status == request.Status.Value.ToDatabaseValue());

        var items = query
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();
        return new SearchAccountsResponse(items, request.Page, request.PageSize);
    }
}
