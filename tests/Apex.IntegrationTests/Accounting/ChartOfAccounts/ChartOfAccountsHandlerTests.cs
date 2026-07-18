using Apex.Application.Abstractions.Exceptions;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.ArchiveAccountClass;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.ArchiveGeneralAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.ArchiveSubsidiaryAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateAccountClass;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateGeneralAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateSubsidiaryAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.GetAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.GetAccountTree;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.ReactivateAccountClass;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.ReactivateGeneralAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.ReactivateSubsidiaryAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.SearchAccounts;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateAccountClass;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateGeneralAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateSubsidiaryAccount;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.IntegrationTests.Accounting.ChartOfAccounts;

[Collection(ApexIntegrationTestCollection.Name)]
public sealed class ChartOfAccountsHandlerTests(ApexIntegrationTestFixture fixture):ApexIntegrationTestBase(fixture)
{
    [Fact]
    public async Task Complete_Workflow_Should_Create_Update_Query_Archive_And_Reactivate_Hierarchy()
    {
        await ResetAccountingDatabaseAsync(); await using var scope=await CreateScopeAsync(); var s=scope.Services;
        var root=await s.GetRequiredService<CreateAccountClassHandler>().HandleAsync(new("  assets ","Assets"),default);
        var general=await s.GetRequiredService<CreateGeneralAccountHandler>().HandleAsync(new(root.Id,"ca","Cash",AccountNature.Debtor),default);
        var leaf=await s.GetRequiredService<CreateSubsidiaryAccountHandler>().HandleAsync(new(general.Id,"bk","Bank",AccountNature.Debtor,DetailAccountType.Bank),default);
        Assert.Equal("ASSETS",root.Code); Assert.Equal("CA",general.Code); Assert.Equal("BK",leaf.Code);

        Assert.Equal("Renamed class",(await s.GetRequiredService<UpdateAccountClassHandler>().HandleAsync(root.Id,new("Renamed class"),default)).Name);
        Assert.Equal("Renamed general",(await s.GetRequiredService<UpdateGeneralAccountHandler>().HandleAsync(general.Id,new("Renamed general"),default)).Name);
        Assert.Equal("Renamed leaf",(await s.GetRequiredService<UpdateSubsidiaryAccountHandler>().HandleAsync(leaf.Id,new("Renamed leaf"),default)).Name);

        var account=await s.GetRequiredService<GetAccountHandler>().HandleAsync(AccountLevel.SubsidiaryAccount,leaf.Id,default);
        Assert.Equal(general.Id,account.ParentId); Assert.Equal("BK",account.Code);
        var tree=await s.GetRequiredService<GetAccountTreeHandler>().HandleAsync(false,default);
        Assert.Equal(leaf.Id,Assert.Single(Assert.Single(Assert.Single(tree).GeneralAccounts).SubsidiaryAccounts).Id);
        var search=await s.GetRequiredService<SearchAccountsHandler>().HandleAsync(new(null,null,"renamed",null,null,null,1,10),default);
        Assert.Equal(3,search.Items.Count);

        await Assert.ThrowsAsync<BusinessRuleException>(()=>s.GetRequiredService<ArchiveAccountClassHandler>().HandleAsync(root.Id,default));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>s.GetRequiredService<ArchiveGeneralAccountHandler>().HandleAsync(general.Id,default));
        await s.GetRequiredService<ArchiveSubsidiaryAccountHandler>().HandleAsync(leaf.Id,default);
        await s.GetRequiredService<ArchiveGeneralAccountHandler>().HandleAsync(general.Id,default);
        await s.GetRequiredService<ArchiveAccountClassHandler>().HandleAsync(root.Id,default);
        Assert.Empty(await s.GetRequiredService<GetAccountTreeHandler>().HandleAsync(false,default));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>s.GetRequiredService<ReactivateSubsidiaryAccountHandler>().HandleAsync(leaf.Id,default));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>s.GetRequiredService<ReactivateGeneralAccountHandler>().HandleAsync(general.Id,default));
        await s.GetRequiredService<ReactivateAccountClassHandler>().HandleAsync(root.Id,default);
        await s.GetRequiredService<ReactivateGeneralAccountHandler>().HandleAsync(general.Id,default);
        await s.GetRequiredService<ReactivateSubsidiaryAccountHandler>().HandleAsync(leaf.Id,default);
        Assert.Single(await s.GetRequiredService<GetAccountTreeHandler>().HandleAsync(false,default));
    }

    [Fact]
    public async Task Create_Should_Reject_Duplicate_Scoped_Codes_And_Inactive_Parent()
    {
        await ResetAccountingDatabaseAsync(); await using var scope=await CreateScopeAsync(); var s=scope.Services;
        var root=await s.GetRequiredService<CreateAccountClassHandler>().HandleAsync(new("A","A"),default);
        var duplicate=await Assert.ThrowsAsync<ConflictException>(()=>s.GetRequiredService<CreateAccountClassHandler>().HandleAsync(new(" a ","Other"),default)); Assert.Equal("account_code_already_exists",duplicate.ErrorCode);
        var general=await s.GetRequiredService<CreateGeneralAccountHandler>().HandleAsync(new(root.Id,"G","G",AccountNature.Neutral),default);
        await s.GetRequiredService<ArchiveGeneralAccountHandler>().HandleAsync(general.Id,default);
        var error=await Assert.ThrowsAsync<BusinessRuleException>(()=>s.GetRequiredService<CreateSubsidiaryAccountHandler>().HandleAsync(new(general.Id,"S","S",AccountNature.Neutral,DetailAccountType.None),default)); Assert.Equal("account_parent_inactive",error.ErrorCode);
    }

    [Fact]
    public async Task Get_Missing_Should_Return_Stable_NotFound_Error()
    {
        await ResetAccountingDatabaseAsync(); await using var scope=await CreateScopeAsync();
        var error=await Assert.ThrowsAsync<NotFoundException>(()=>scope.Services.GetRequiredService<GetAccountHandler>().HandleAsync(AccountLevel.AccountClass,999,default));
        Assert.Equal("account_class_not_found",error.ErrorCode);
    }
}
