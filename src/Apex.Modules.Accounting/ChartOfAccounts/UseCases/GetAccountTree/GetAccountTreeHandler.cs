using Apex.Modules.Accounting.ChartOfAccounts.Repositories;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.GetAccountTree;

internal sealed class GetAccountTreeHandler(
    IAccountClassReadRepository classes, IGeneralAccountReadRepository generals,
    ISubsidiaryAccountReadRepository subsidiaries)
{
    public async Task<IReadOnlyList<ClassNode>> HandleAsync(bool includeArchived, CancellationToken ct)
    {
        var classRows = await classes.ListAsync(includeArchived, ct);
        var generalRows = await generals.ListAsync(includeArchived, ct);
        var subsidiaryRows = await subsidiaries.ListAsync(includeArchived, ct);

        var subsidiariesByGeneral = subsidiaryRows
            .GroupBy(x => x.GeneralAccountId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SubsidiaryNode>)g
                .Select(s => new SubsidiaryNode(s.Id, s.Code, s.Name, s.Nature, s.DetailAccountType, s.Status))
                .ToList());

        var generalsByClass = generalRows
            .GroupBy(x => x.AccountClassId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<GeneralNode>)g
                .Select(a => new GeneralNode(a.Id, a.Code, a.Name, a.Nature, a.Status,
                    subsidiariesByGeneral.GetValueOrDefault(a.Id, [])))
                .ToList());

        return classRows
            .Select(a => new ClassNode(a.Id, a.Code, a.Name, a.Status, generalsByClass.GetValueOrDefault(a.Id, [])))
            .ToList();
    }
}
