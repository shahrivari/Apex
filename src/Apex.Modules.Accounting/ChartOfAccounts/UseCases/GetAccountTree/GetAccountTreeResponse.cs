namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.GetAccountTree;

internal sealed record SubsidiaryNode(long Id, string Code, string Name, string Nature, string DetailAccountType, string Status);

internal sealed record GeneralNode(
    long Id, string Code, string Name, string Nature, string Status,
    IReadOnlyList<SubsidiaryNode> SubsidiaryAccounts);

internal sealed record ClassNode(
    long Id, string Code, string Name, string Status, IReadOnlyList<GeneralNode> GeneralAccounts);
