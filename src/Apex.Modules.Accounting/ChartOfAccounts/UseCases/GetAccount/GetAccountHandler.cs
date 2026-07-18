using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.GetAccount;

internal sealed class GetAccountHandler(
    IAccountClassReadRepository classes, IGeneralAccountReadRepository generals,
    ISubsidiaryAccountReadRepository subsidiaries)
{
    public async Task<GetAccountResponse> HandleAsync(AccountLevel level, long id, CancellationToken ct) => level switch
    {
        AccountLevel.AccountClass => Map(await classes.GetAsync(id, ct)
            ?? throw new NotFoundException("Account class was not found.", ChartOfAccountsErrors.AccountClassNotFound)),
        AccountLevel.GeneralAccount => Map(await generals.GetAsync(id, ct)
            ?? throw new NotFoundException("General account was not found.", ChartOfAccountsErrors.GeneralAccountNotFound)),
        AccountLevel.SubsidiaryAccount => Map(await subsidiaries.GetAsync(id, ct)
            ?? throw new NotFoundException("Subsidiary account was not found.", ChartOfAccountsErrors.SubsidiaryAccountNotFound)),
        _ => throw new BusinessRuleException("Unknown account level.", ChartOfAccountsErrors.InvalidLevel)
    };

    private static GetAccountResponse Map(AccountClassRow row) =>
        new(row.Id, "ACCOUNT_CLASS", null, row.Code, row.Name, null, null, row.Status, row.CreatedAt, row.UpdatedAt, row.ArchivedAt);

    private static GetAccountResponse Map(GeneralAccountRow row) =>
        new(row.Id, "GENERAL_ACCOUNT", row.AccountClassId, row.Code, row.Name, row.Nature, null, row.Status, row.CreatedAt, row.UpdatedAt, row.ArchivedAt);

    private static GetAccountResponse Map(SubsidiaryAccountRow row) =>
        new(row.Id, "SUBSIDIARY_ACCOUNT", row.GeneralAccountId, row.Code, row.Name, row.Nature, row.DetailAccountType, row.Status, row.CreatedAt, row.UpdatedAt, row.ArchivedAt);
}
