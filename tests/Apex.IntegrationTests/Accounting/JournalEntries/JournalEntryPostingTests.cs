using Apex.Application.Abstractions.Exceptions;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ActivateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateAccountClass;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateGeneralAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateSubsidiaryAccount;
using Apex.Modules.Accounting.FiscalYears.UseCases.CreateFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.OpenFiscalYear;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using Apex.Modules.Accounting.JournalEntries.UseCases.CreateDraftJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.PostJournalEntry;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.IntegrationTests.Accounting.JournalEntries;

[Collection(ApexIntegrationTestCollection.Name)]
public sealed class JournalEntryPostingTests(ApexIntegrationTestFixture fixture)
    : ApexIntegrationTestBase(fixture)
{
    private static readonly DateOnly AccountingDate = new(2026, 6, 1);

    [Fact]
    public async Task Post_FinancialEntry_MarksPostedAndUpdatesProjections()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-POST-FIN", "je-post-fin");
        var created = await CreateAsync(scope, setup, BalanceEffect.Financial);

        var posted = await scope.Services.GetRequiredService<PostJournalEntryHandler>()
            .HandleAsync(setup.FiscalYearId, created.Id);

        Assert.Equal("POSTED", posted.Status);
        Assert.NotNull(posted.PostedAt);

        var projections = scope.Services.GetRequiredService<IJournalEntryProjectionReadRepository>();
        var debitTurnover = await projections.GetTurnoverAsync(
            setup.BookId, setup.FiscalYearId, AccountingDate, setup.ClassCode, setup.GeneralCode,
            setup.DebitSubCode, null, "GENERAL");
        var creditTurnover = await projections.GetTurnoverAsync(
            setup.BookId, setup.FiscalYearId, AccountingDate, setup.ClassCode, setup.GeneralCode,
            setup.CreditSubCode, null, "GENERAL");

        Assert.NotNull(debitTurnover);
        Assert.Equal(100m, debitTurnover.DebitTurnover);
        Assert.Equal(0m, debitTurnover.CreditTurnover);
        Assert.Equal(100m, debitTurnover.NetTurnover);
        Assert.NotNull(creditTurnover);
        Assert.Equal(100m, creditTurnover.CreditTurnover);
        Assert.Equal(-100m, creditTurnover.NetTurnover);

        var debitBalance = await projections.GetClosingBalanceAsOfAsync(
            setup.BookId, setup.FiscalYearId, AccountingDate, setup.ClassCode, setup.GeneralCode,
            setup.DebitSubCode, null);
        var creditBalance = await projections.GetClosingBalanceAsOfAsync(
            setup.BookId, setup.FiscalYearId, AccountingDate, setup.ClassCode, setup.GeneralCode,
            setup.CreditSubCode, null);
        Assert.Equal(100m, debitBalance);
        Assert.Equal(-100m, creditBalance);
    }

    [Fact]
    public async Task Post_StatisticalEntry_DoesNotUpdateFinancialProjections()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-POST-STAT", "je-post-stat");
        var created = await CreateAsync(scope, setup, BalanceEffect.Statistical);

        var posted = await scope.Services.GetRequiredService<PostJournalEntryHandler>()
            .HandleAsync(setup.FiscalYearId, created.Id);

        Assert.Equal("POSTED", posted.Status);
        var projections = scope.Services.GetRequiredService<IJournalEntryProjectionReadRepository>();
        Assert.Null(await projections.GetTurnoverAsync(
            setup.BookId, setup.FiscalYearId, AccountingDate, setup.ClassCode, setup.GeneralCode,
            setup.DebitSubCode, null, "GENERAL"));
        Assert.Equal(0m, await projections.GetClosingBalanceAsOfAsync(
            setup.BookId, setup.FiscalYearId, AccountingDate, setup.ClassCode, setup.GeneralCode,
            setup.DebitSubCode, null));
    }

    [Fact]
    public async Task Post_UnbalancedEntry_IsRejected()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-POST-UNBAL", "je-post-unbal");
        var created = await scope.Services.GetRequiredService<CreateDraftJournalEntryHandler>().HandleAsync(
            Request(setup, BalanceEffect.Financial, debitAmount: 100m, creditAmount: 60m));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            scope.Services.GetRequiredService<PostJournalEntryHandler>().HandleAsync(setup.FiscalYearId, created.Id));

        Assert.Equal(JournalEntryErrors.Unbalanced, exception.ErrorCode);
    }

    [Fact]
    public async Task Post_WithNonExistentAccountPath_IsRejected()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        // Fiscal year only; no chart-of-accounts rows, so the line account paths do not exist.
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync(scope, "JE-POST-NOACCT", "je-post-noacct");
        var request = new CreateDraftJournalEntryRequest
        {
            AccountingBookId = bookId,
            FiscalYearId = fiscalYearId,
            AccountingDate = AccountingDate,
            Description = "entry",
            DocumentType = "GENERAL",
            InsertionType = "MANUAL",
            BalanceEffect = "FINANCIAL",
            Lines = [Line("DEBIT", 100m, "9", "99", "99"), Line("CREDIT", 100m, "9", "99", "99")]
        };
        var created = await scope.Services.GetRequiredService<CreateDraftJournalEntryHandler>().HandleAsync(request);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            scope.Services.GetRequiredService<PostJournalEntryHandler>().HandleAsync(fiscalYearId, created.Id));

        Assert.Equal(JournalEntryErrors.InvalidAccountCodePath, exception.ErrorCode);
    }

    [Fact]
    public async Task Post_Twice_IsRejectedAndDoesNotDoubleApplyProjections()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-POST-TWICE", "je-post-twice");
        var created = await CreateAsync(scope, setup, BalanceEffect.Financial);
        var handler = scope.Services.GetRequiredService<PostJournalEntryHandler>();
        await handler.HandleAsync(setup.FiscalYearId, created.Id);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.HandleAsync(setup.FiscalYearId, created.Id));

        Assert.Equal(JournalEntryErrors.DraftRequired, exception.ErrorCode);
        var turnover = await scope.Services.GetRequiredService<IJournalEntryProjectionReadRepository>()
            .GetTurnoverAsync(setup.BookId, setup.FiscalYearId, AccountingDate, setup.ClassCode,
                setup.GeneralCode, setup.DebitSubCode, null, "GENERAL");
        Assert.Equal(100m, turnover!.DebitTurnover);
    }

    private async Task<JournalEntryDetailResponse> CreateAsync(
        ServiceScopeHandle scope, AccountSetup setup, BalanceEffect balanceEffect) =>
        await scope.Services.GetRequiredService<CreateDraftJournalEntryHandler>()
            .HandleAsync(Request(setup, balanceEffect, 100m, 100m));

    private static CreateDraftJournalEntryRequest Request(
        AccountSetup setup, BalanceEffect balanceEffect, decimal debitAmount, decimal creditAmount) => new()
        {
            AccountingBookId = setup.BookId,
            FiscalYearId = setup.FiscalYearId,
            AccountingDate = AccountingDate,
            Description = "entry",
            DocumentType = "GENERAL",
            InsertionType = "MANUAL",
            BalanceEffect = balanceEffect.ToDatabaseValue(),
            Lines =
        [
            Line("DEBIT", debitAmount, setup.ClassCode, setup.GeneralCode, setup.DebitSubCode),
            Line("CREDIT", creditAmount, setup.ClassCode, setup.GeneralCode, setup.CreditSubCode)
        ]
        };

    private static JournalEntryLineRequest Line(
        string side, decimal amount, string classCode, string generalCode, string subCode) => new()
        {
            Side = side,
            Amount = amount,
            AccountClassCode = classCode,
            GeneralAccountCode = generalCode,
            SubsidiaryAccountCode = subCode,
            Description = "line"
        };

    private async Task ResetDatabasesAsync()
    {
        await ResetAccountingDatabaseAsync();
        await ResetShardDatabaseAsync();
    }

    private async Task<AccountSetup> SetupAsync(ServiceScopeHandle scope, string code, string ownerId)
    {
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync(scope, code, ownerId);
        var s = scope.Services;
        var accountClass = await s.GetRequiredService<CreateAccountClassHandler>()
            .HandleAsync(new CreateAccountClassRequest("1", "Assets"), default);
        var general = await s.GetRequiredService<CreateGeneralAccountHandler>()
            .HandleAsync(new CreateGeneralAccountRequest(accountClass.Id, "01", "Cash", AccountNature.Debtor), default);
        var debit = await s.GetRequiredService<CreateSubsidiaryAccountHandler>().HandleAsync(
            new CreateSubsidiaryAccountRequest(general.Id, "01", "Debit", AccountNature.Debtor, DetailAccountType.None),
            default);
        var credit = await s.GetRequiredService<CreateSubsidiaryAccountHandler>().HandleAsync(
            new CreateSubsidiaryAccountRequest(general.Id, "02", "Credit", AccountNature.Creditor, DetailAccountType.None),
            default);
        return new AccountSetup(bookId, fiscalYearId, accountClass.Code, general.Code, debit.Code, credit.Code);
    }

    private async Task<(long BookId, long FiscalYearId)> CreateOpenFiscalYearAsync(
        ServiceScopeHandle scope, string code, string ownerId)
    {
        var book = await scope.Services.GetRequiredService<CreateAccountingBookHandler>().HandleAsync(
            new CreateAccountingBookRequest { Code = code, Title = code, OwnerType = "TEST", OwnerId = ownerId });
        await scope.Services.GetRequiredService<ActivateAccountingBookHandler>().HandleAsync(book.Id);
        var fiscalYear = await scope.Services.GetRequiredService<CreateFiscalYearHandler>().HandleAsync(
            new CreateFiscalYearRequest
            {
                AccountingBookId = book.Id,
                Title = "2026",
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31)
            });
        await scope.Services.GetRequiredService<OpenFiscalYearHandler>().HandleAsync(fiscalYear.Id);
        return (book.Id, fiscalYear.Id);
    }

    private sealed record AccountSetup(
        long BookId, long FiscalYearId, string ClassCode, string GeneralCode,
        string DebitSubCode, string CreditSubCode);
}
