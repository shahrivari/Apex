using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;

namespace Apex.UnitTests.Accounting.ChartOfAccounts;

public sealed class ChartOfAccountsDomainTests
{
    private static readonly DateTime Now = new(2026, 7, 17, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_Should_Normalize_Code_And_Start_Active()
    {
        var accountClass = AccountClass.Create(1, "  assets  ", " Assets ", Now);
        var general = GeneralAccount.Create(2, 1, " ca ", " Cash ", AccountNature.Debtor, Now);
        var subsidiary = SubsidiaryAccount.Create(3, 2, " bk ", " Bank ", AccountNature.Debtor, DetailAccountType.Bank, Now);
        Assert.Equal("ASSETS", accountClass.Code);
        Assert.Equal("CA", general.Code);
        Assert.Equal("BK", subsidiary.Code);
        Assert.Equal(AccountStatus.Active, accountClass.Status);
        Assert.Equal(AccountStatus.Active, general.Status);
        Assert.Equal(AccountStatus.Active, subsidiary.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Reject_Blank_Code(string code) => Assert.Throws<BusinessRuleException>(() => AccountClass.Create(1, code, "Name", Now));

    [Fact]
    public void Create_Should_Reject_Overlong_Code_And_Name()
    {
        Assert.Throws<BusinessRuleException>(() => AccountClass.Create(1, new string('A', 65), "Name", Now));
        Assert.Throws<BusinessRuleException>(() => GeneralAccount.Create(2, 1, new string('G', 3), "Name", AccountNature.Debtor, Now));
        Assert.Throws<BusinessRuleException>(() => SubsidiaryAccount.Create(3, 2, new string('S', 3), "Name", AccountNature.Debtor, DetailAccountType.Person, Now));
        Assert.Throws<BusinessRuleException>(() => AccountClass.Create(1, "A", new string('N', 256), Now));
    }

    [Fact]
    public void Lifecycle_Should_Archive_Reactivate_And_Reject_Repeated_Transitions()
    {
        var value = AccountClass.Create(1, "A", "Name", Now);
        value.Archive(Now.AddMinutes(1));
        Assert.Equal(AccountStatus.Archived, value.Status);
        Assert.NotNull(value.ArchivedAt);
        Assert.Equal("account_already_archived", Assert.Throws<BusinessRuleException>(() => value.Archive(Now)).ErrorCode);
        value.Reactivate(Now.AddMinutes(2));
        Assert.Equal(AccountStatus.Active, value.Status);
        Assert.Null(value.ArchivedAt);
        Assert.Equal("account_already_active", Assert.Throws<BusinessRuleException>(() => value.Reactivate(Now)).ErrorCode);
    }

    [Fact]
    public void Rename_Should_Change_Only_Name()
    {
        var value = SubsidiaryAccount.Create(3, 2, "BK", "Old", AccountNature.Debtor, DetailAccountType.Bank, Now);
        value.Rename(" New ", Now.AddMinutes(1));
        Assert.Equal("New", value.Name);
        Assert.Equal("BK", value.Code);
        Assert.Equal(2, value.GeneralAccountId);
        Assert.Equal(AccountNature.Debtor, value.Nature);
        Assert.Equal(DetailAccountType.Bank, value.DetailAccountType);
    }
}
