using Apex.Application.Abstractions.Data;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.IntegrationTests.Accounting.ChartOfAccounts;

[Collection(ApexIntegrationTestCollection.Name)]
public sealed class ChartOfAccountsRepositoryContractTests(ApexIntegrationTestFixture fixture) : ApexIntegrationTestBase(fixture)
{
    [Fact]
    public async Task Repositories_Should_RoundTrip_All_Entities_In_Deterministic_Order()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var s = scope.Services;
        var classWrite = s.GetRequiredService<IAccountClassWriteRepository>();
        var generalWrite = s.GetRequiredService<IGeneralAccountWriteRepository>();
        var subsidiaryWrite = s.GetRequiredService<ISubsidiaryAccountWriteRepository>();
        var now = new DateTime(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc);
        await classWrite.InsertAsync(AccountClass.Create(10, "B", "B", now));
        await classWrite.InsertAsync(AccountClass.Create(11, "A", "A", now));
        await generalWrite.InsertAsync(GeneralAccount.Create(20, 11, "G", "General", AccountNature.Creditor, now));
        await subsidiaryWrite.InsertAsync(SubsidiaryAccount.Create(30, 20, "S", "Subsidiary", AccountNature.Debtor, DetailAccountType.Person, now));
        var roots = await s.GetRequiredService<IAccountClassReadRepository>().ListAsync(false);
        Assert.Equal(["A", "B"], roots.Select(x => x.Code));
        var general = await s.GetRequiredService<IGeneralAccountReadRepository>().GetAsync(20);
        Assert.NotNull(general);
        Assert.Equal("CREDITOR", general.Nature);
        var leaf = await s.GetRequiredService<ISubsidiaryAccountReadRepository>().GetAsync(30);
        Assert.NotNull(leaf);
        Assert.Equal("PERSON", leaf.DetailAccountType);
    }

    [Fact]
    public async Task Database_Should_Enforce_Code_Scopes_Enums_And_Foreign_Keys()
    {
        await ResetAccountingDatabaseAsync();
        await using var connection = CreateAccountingConnection();
        await connection.OpenAsync();
        var now = DateTime.UtcNow;
        await connection.ExecuteAsync("INSERT INTO account_class(id,code,name,status,created_at) VALUES(1,'A','A','ACTIVE',@Now),(2,'B','B','ACTIVE',@Now)", new { Now = now });
        await Assert.ThrowsAsync<SqlException>(() => connection.ExecuteAsync("INSERT INTO account_class(id,code,name,status,created_at) VALUES(3,'A','Duplicate','ACTIVE',@Now)", new { Now = now }));
        await connection.ExecuteAsync("INSERT INTO general_account(id,account_class_id,code,name,nature,status,created_at) VALUES(10,1,'G','G','DEBTOR','ACTIVE',@Now),(11,2,'G','G','CREDITOR','ACTIVE',@Now)", new { Now = now });
        await Assert.ThrowsAsync<SqlException>(() => connection.ExecuteAsync("INSERT INTO general_account(id,account_class_id,code,name,nature,status,created_at) VALUES(12,1,'G','Duplicate','NEUTRAL','ACTIVE',@Now)", new { Now = now }));
        await Assert.ThrowsAsync<SqlException>(() => connection.ExecuteAsync("INSERT INTO subsidiary_account(id,general_account_id,code,name,nature,detail_account_type,status,created_at) VALUES(20,999,'S','S','INVALID','INVALID','ACTIVE',@Now)", new { Now = now }));
    }

    [Fact]
    public async Task Write_Repository_Should_Participate_In_Rollback()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var s = scope.Services;
        var runner = s.GetRequiredService<IGeneralTransactionRunner>();
        var repository = s.GetRequiredService<IAccountClassWriteRepository>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.ExecuteAsync(async ct => { await repository.InsertAsync(AccountClass.Create(100, "RB", "Rollback", DateTime.UtcNow), ct); throw new InvalidOperationException("force rollback"); }));
        Assert.Null(await s.GetRequiredService<IAccountClassReadRepository>().GetAsync(100));
    }
}
