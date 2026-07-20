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
using Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.GetFiscalYear;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using Apex.Modules.Accounting.JournalEntries.UseCases.CreateDraftJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.PostJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.GetJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.ReverseJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.SearchJournalEntries;
using Microsoft.Extensions.DependencyInjection;
using Dapper;

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

    [Fact]
    public async Task Reverse_FinancialEntry_PostsOppositeEffectsAndLinksOriginal()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-REVERSE-FIN", "je-reverse-fin");
        var created = await CreateAsync(scope, setup, BalanceEffect.Financial);
        var original = await scope.Services.GetRequiredService<PostJournalEntryHandler>()
            .HandleAsync(setup.FiscalYearId, created.Id);
        var reversalDate = AccountingDate.AddDays(1);

        var reversal = await scope.Services.GetRequiredService<ReverseJournalEntryHandler>().HandleAsync(
            setup.FiscalYearId, original.ReferenceNumber,
            new ReverseJournalEntryRequest
            {
                AccountingDate = reversalDate,
                ReversalReason = "Incorrect posting"
            });

        Assert.Equal("POSTED", reversal.Status);
        Assert.Equal("SYSTEM", reversal.InsertionType);
        Assert.Equal(original.ReferenceNumber, reversal.ReversalOfReferenceNumber);
        Assert.Equal(["CREDIT", "DEBIT"], reversal.Lines.Select(line => line.Side).ToArray());
        var reloadedOriginal = await scope.Services.GetRequiredService<GetJournalEntryHandler>()
            .GetByIdAsync(setup.FiscalYearId, original.Id);
        Assert.Equal(reversal.ReferenceNumber, reloadedOriginal.ReversedByReferenceNumber);

        var projections = scope.Services.GetRequiredService<IJournalEntryProjectionReadRepository>();
        Assert.Equal(0m, await projections.GetClosingBalanceAsOfAsync(
            setup.BookId, setup.FiscalYearId, reversalDate, setup.ClassCode, setup.GeneralCode,
            setup.DebitSubCode, null));
        Assert.Equal(0m, await projections.GetClosingBalanceAsOfAsync(
            setup.BookId, setup.FiscalYearId, reversalDate, setup.ClassCode, setup.GeneralCode,
            setup.CreditSubCode, null));
    }

    [Fact]
    public async Task Reverse_Twice_IsRejectedWithoutAdditionalProjectionEffects()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-REVERSE-TWICE", "je-reverse-twice");
        var created = await CreateAsync(scope, setup, BalanceEffect.Financial);
        var original = await scope.Services.GetRequiredService<PostJournalEntryHandler>()
            .HandleAsync(setup.FiscalYearId, created.Id);
        var handler = scope.Services.GetRequiredService<ReverseJournalEntryHandler>();
        var request = new ReverseJournalEntryRequest
        {
            AccountingDate = AccountingDate.AddDays(1),
            ReversalReason = "Incorrect posting"
        };
        await handler.HandleAsync(setup.FiscalYearId, original.ReferenceNumber, request);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(setup.FiscalYearId, original.ReferenceNumber, request));

        Assert.Equal(JournalEntryErrors.AlreadyReversed, exception.ErrorCode);
        Assert.Equal(0m, await scope.Services.GetRequiredService<IJournalEntryProjectionReadRepository>()
            .GetClosingBalanceAsOfAsync(
                setup.BookId, setup.FiscalYearId, request.AccountingDate, setup.ClassCode,
                setup.GeneralCode, setup.DebitSubCode, null));
    }

    [Fact]
    public async Task Reverse_StatisticalEntry_DoesNotCreateFinancialProjections()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-REVERSE-STAT", "je-reverse-stat");
        var created = await CreateAsync(scope, setup, BalanceEffect.Statistical);
        var original = await scope.Services.GetRequiredService<PostJournalEntryHandler>()
            .HandleAsync(setup.FiscalYearId, created.Id);

        var reversal = await scope.Services.GetRequiredService<ReverseJournalEntryHandler>().HandleAsync(
            setup.FiscalYearId, original.ReferenceNumber,
            new ReverseJournalEntryRequest
            {
                AccountingDate = AccountingDate.AddDays(1),
                ReversalReason = "Incorrect statistical posting"
            });

        Assert.Equal("POSTED", reversal.Status);
        var projections = scope.Services.GetRequiredService<IJournalEntryProjectionReadRepository>();
        Assert.Null(await projections.GetTurnoverAsync(
            setup.BookId, setup.FiscalYearId, reversal.AccountingDate, setup.ClassCode,
            setup.GeneralCode, setup.DebitSubCode, null, "GENERAL"));
        Assert.Equal(0m, await projections.GetClosingBalanceAsOfAsync(
            setup.BookId, setup.FiscalYearId, reversal.AccountingDate, setup.ClassCode,
            setup.GeneralCode, setup.DebitSubCode, null));
    }

    [Fact]
    public async Task Reverse_WithEarlierDate_RollsBackNumberAllocationAndLinks()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-REVERSE-DATE", "je-reverse-date");
        var created = await CreateAsync(scope, setup, BalanceEffect.Financial);
        var original = await scope.Services.GetRequiredService<PostJournalEntryHandler>()
            .HandleAsync(setup.FiscalYearId, created.Id);
        var handler = scope.Services.GetRequiredService<ReverseJournalEntryHandler>();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.HandleAsync(
            setup.FiscalYearId, original.ReferenceNumber,
            new ReverseJournalEntryRequest
            {
                AccountingDate = AccountingDate.AddDays(-1),
                ReversalReason = "Invalid correction"
            }));
        Assert.Equal(JournalEntryErrors.InvalidReversalDate, exception.ErrorCode);

        var reversal = await handler.HandleAsync(
            setup.FiscalYearId, original.ReferenceNumber,
            new ReverseJournalEntryRequest
            {
                AccountingDate = AccountingDate.AddDays(1),
                ReversalReason = "Valid correction"
            });
        Assert.Equal(2, reversal.ReferenceNumber);
        Assert.Equal(2, reversal.JournalEntryNumber);
    }

    [Fact]
    public async Task Reverse_Draft_IsRejectedWithoutConsumingNumbers()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-REVERSE-DRAFT", "je-reverse-draft");
        var draft = await CreateAsync(scope, setup, BalanceEffect.Financial);
        var reversalHandler = scope.Services.GetRequiredService<ReverseJournalEntryHandler>();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => reversalHandler.HandleAsync(
            setup.FiscalYearId, draft.ReferenceNumber,
            new ReverseJournalEntryRequest
            {
                AccountingDate = AccountingDate.AddDays(1),
                ReversalReason = "Cannot reverse a draft"
            }));
        Assert.Equal(JournalEntryErrors.PostedImmutable, exception.ErrorCode);

        var posted = await scope.Services.GetRequiredService<PostJournalEntryHandler>()
            .HandleAsync(setup.FiscalYearId, draft.Id);
        var reversal = await reversalHandler.HandleAsync(
            setup.FiscalYearId, posted.ReferenceNumber,
            new ReverseJournalEntryRequest
            {
                AccountingDate = AccountingDate.AddDays(1),
                ReversalReason = "Valid correction"
            });
        Assert.Equal(2, reversal.ReferenceNumber);
    }

    [Fact]
    public async Task ConcurrentReversals_OnlyOneCommits()
    {
        await ResetDatabasesAsync();
        AccountSetup setup;
        long originalReferenceNumber;
        await using (var setupScope = await CreateScopeAsync())
        {
            setup = await SetupAsync(setupScope, "JE-REVERSE-CONCURRENT", "je-reverse-concurrent");
            var created = await CreateAsync(setupScope, setup, BalanceEffect.Financial);
            var original = await setupScope.Services.GetRequiredService<PostJournalEntryHandler>()
                .HandleAsync(setup.FiscalYearId, created.Id);
            originalReferenceNumber = original.ReferenceNumber;
        }

        var attempts = Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var scope = await CreateScopeAsync();
            try
            {
                await scope.Services.GetRequiredService<ReverseJournalEntryHandler>().HandleAsync(
                    setup.FiscalYearId, originalReferenceNumber,
                    new ReverseJournalEntryRequest
                    {
                        AccountingDate = AccountingDate.AddDays(1),
                        ReversalReason = "Concurrent correction"
                    });
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        });

        var results = await Task.WhenAll(attempts);
        Assert.Single(results, result => result is null);
        var conflict = Assert.Single(results.OfType<ConflictException>());
        Assert.Equal(JournalEntryErrors.AlreadyReversed, conflict.ErrorCode);
        await using var verificationScope = await CreateScopeAsync();
        Assert.Equal(0m, await verificationScope.Services
            .GetRequiredService<IJournalEntryProjectionReadRepository>()
            .GetClosingBalanceAsOfAsync(
                setup.BookId, setup.FiscalYearId, AccountingDate.AddDays(1), setup.ClassCode,
                setup.GeneralCode, setup.DebitSubCode, null));
    }

    [Fact]
    public async Task Reverse_WithWrongFiscalYearRoute_CannotAccessOriginalOrConsumeNumbers()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var first = await SetupAsync(scope, "JE-REVERSE-ROUTE-1", "je-reverse-route-1");
        var (_, secondFiscalYearId) = await CreateOpenFiscalYearAsync(
            scope, "JE-REVERSE-ROUTE-2", "je-reverse-route-2");
        var created = await CreateAsync(scope, first, BalanceEffect.Financial);
        var original = await scope.Services.GetRequiredService<PostJournalEntryHandler>()
            .HandleAsync(first.FiscalYearId, created.Id);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Services.GetRequiredService<ReverseJournalEntryHandler>().HandleAsync(
                secondFiscalYearId, original.ReferenceNumber,
                new ReverseJournalEntryRequest
                {
                    AccountingDate = AccountingDate.AddDays(1),
                    ReversalReason = "Wrong partition"
                }));

        Assert.Equal(JournalEntryErrors.NotFound, exception.ErrorCode);
        var reversal = await scope.Services.GetRequiredService<ReverseJournalEntryHandler>().HandleAsync(
            first.FiscalYearId, original.ReferenceNumber,
            new ReverseJournalEntryRequest
            {
                AccountingDate = AccountingDate.AddDays(1),
                ReversalReason = "Correct partition"
            });
        Assert.Equal(2, reversal.ReferenceNumber);
        Assert.Equal(2, reversal.JournalEntryNumber);
    }

    [Fact]
    public async Task FinalizeNextDay_RenumbersTailAndFreezesEntriesThroughTarget()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-FINALIZE", "je-finalize");
        var targetDate = new DateOnly(2026, 1, 1);
        var later = await scope.Services.GetRequiredService<CreateDraftJournalEntryHandler>()
            .HandleAsync(Request(setup, BalanceEffect.Financial, 100m, 100m, targetDate.AddDays(1)));
        var target = await scope.Services.GetRequiredService<CreateDraftJournalEntryHandler>()
            .HandleAsync(Request(setup, BalanceEffect.Financial, 100m, 100m, targetDate));
        var posting = scope.Services.GetRequiredService<PostJournalEntryHandler>();
        await posting.HandleAsync(setup.FiscalYearId, later.Id);
        await posting.HandleAsync(setup.FiscalYearId, target.Id);

        var result = await scope.Services.GetRequiredService<FinalizeFiscalYearHandler>().HandleAsync(
            setup.FiscalYearId,
            new FinalizeFiscalYearRequest { FinalizedThroughDate = targetDate });

        Assert.Equal(targetDate, result.FinalizedThroughDate);
        var get = scope.Services.GetRequiredService<GetJournalEntryHandler>();
        var finalized = await get.GetByIdAsync(setup.FiscalYearId, target.Id);
        var provisional = await get.GetByIdAsync(setup.FiscalYearId, later.Id);
        Assert.Equal(1, finalized.JournalEntryNumber);
        Assert.True(finalized.NumberFinalized);
        Assert.Equal(2, provisional.JournalEntryNumber);
        Assert.False(provisional.NumberFinalized);
    }

    [Fact]
    public async Task FinalizeNextDay_WithDraftOnTarget_IsRejectedWithoutAdvancingBoundary()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-FINALIZE-DRAFT", "je-finalize-draft");
        var targetDate = new DateOnly(2026, 1, 1);
        await scope.Services.GetRequiredService<CreateDraftJournalEntryHandler>()
            .HandleAsync(Request(setup, BalanceEffect.Financial, 100m, 100m, targetDate));

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            scope.Services.GetRequiredService<FinalizeFiscalYearHandler>().HandleAsync(
                setup.FiscalYearId,
                new FinalizeFiscalYearRequest { FinalizedThroughDate = targetDate }));

        Assert.Equal(JournalEntryErrors.DraftsBlockFinalization, exception.ErrorCode);
        var fiscalYear = await scope.Services.GetRequiredService<GetFiscalYearHandler>()
            .HandleAsync(setup.FiscalYearId);
        Assert.Equal(targetDate.AddDays(-1), fiscalYear.FinalizedThroughDate);
    }

    [Fact]
    public async Task FinalizeNextDay_RenumbersLaterDraftButLeavesItProvisional()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-FINALIZE-LATER-DRAFT", "je-finalize-later-draft");
        var targetDate = new DateOnly(2026, 1, 1);
        var laterDraft = await scope.Services.GetRequiredService<CreateDraftJournalEntryHandler>()
            .HandleAsync(Request(setup, BalanceEffect.Financial, 100m, 100m, targetDate.AddDays(1)));
        var target = await scope.Services.GetRequiredService<CreateDraftJournalEntryHandler>()
            .HandleAsync(Request(setup, BalanceEffect.Financial, 100m, 100m, targetDate));
        await scope.Services.GetRequiredService<PostJournalEntryHandler>()
            .HandleAsync(setup.FiscalYearId, target.Id);

        await scope.Services.GetRequiredService<FinalizeFiscalYearHandler>().HandleAsync(
            setup.FiscalYearId,
            new FinalizeFiscalYearRequest { FinalizedThroughDate = targetDate });

        var get = scope.Services.GetRequiredService<GetJournalEntryHandler>();
        var finalized = await get.GetByIdAsync(setup.FiscalYearId, target.Id);
        var provisional = await get.GetByIdAsync(setup.FiscalYearId, laterDraft.Id);
        Assert.Equal(1, finalized.JournalEntryNumber);
        Assert.True(finalized.NumberFinalized);
        Assert.Equal(2, provisional.JournalEntryNumber);
        Assert.False(provisional.NumberFinalized);
        Assert.Equal("DRAFT", provisional.Status);
    }

    [Fact]
    public async Task Finalize_CurrentBoundary_IsIdempotentAndDoesNotRenumberTailAgain()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-FINALIZE-IDEMPOTENT", "je-finalize-idempotent");
        var targetDate = new DateOnly(2026, 1, 1);
        var created = await scope.Services.GetRequiredService<CreateDraftJournalEntryHandler>()
            .HandleAsync(Request(setup, BalanceEffect.Statistical, 100m, 100m, targetDate));
        await scope.Services.GetRequiredService<PostJournalEntryHandler>()
            .HandleAsync(setup.FiscalYearId, created.Id);
        var handler = scope.Services.GetRequiredService<FinalizeFiscalYearHandler>();
        var request = new FinalizeFiscalYearRequest { FinalizedThroughDate = targetDate };
        var first = await handler.HandleAsync(setup.FiscalYearId, request);
        var beforeReplay = await scope.Services.GetRequiredService<GetJournalEntryHandler>()
            .GetByIdAsync(setup.FiscalYearId, created.Id);

        var replay = await handler.HandleAsync(setup.FiscalYearId, request);
        var afterReplay = await scope.Services.GetRequiredService<GetJournalEntryHandler>()
            .GetByIdAsync(setup.FiscalYearId, created.Id);

        Assert.Equal(first.FinalizedThroughDate, replay.FinalizedThroughDate);
        Assert.Equal(beforeReplay.JournalEntryNumber, afterReplay.JournalEntryNumber);
        Assert.True(afterReplay.NumberFinalized);
    }

    [Fact]
    public async Task Reverse_OnFinalizedDate_IsRejectedWithoutCreatingEntry()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-REVERSE-FINALIZED", "je-reverse-finalized");
        var targetDate = new DateOnly(2026, 1, 1);
        var created = await scope.Services.GetRequiredService<CreateDraftJournalEntryHandler>()
            .HandleAsync(Request(setup, BalanceEffect.Statistical, 100m, 100m, targetDate));
        var original = await scope.Services.GetRequiredService<PostJournalEntryHandler>()
            .HandleAsync(setup.FiscalYearId, created.Id);
        await scope.Services.GetRequiredService<FinalizeFiscalYearHandler>().HandleAsync(
            setup.FiscalYearId,
            new FinalizeFiscalYearRequest { FinalizedThroughDate = targetDate });

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            scope.Services.GetRequiredService<ReverseJournalEntryHandler>().HandleAsync(
                setup.FiscalYearId, original.ReferenceNumber,
                new ReverseJournalEntryRequest
                {
                    AccountingDate = targetDate,
                    ReversalReason = "Too late"
                }));

        Assert.Equal(JournalEntryErrors.AccountingDateFinalized, exception.ErrorCode);
        var search = await scope.Services.GetRequiredService<SearchJournalEntriesHandler>()
            .HandleAsync(new SearchJournalEntriesRequest
            {
                FiscalYearId = setup.FiscalYearId,
                Page = 1,
                PageSize = 10
            });
        Assert.Single(search.Items);
    }

    [Fact]
    public async Task FinalizeNextDay_WithProjectionDrift_IsRejectedWithoutFreezingNumber()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-FINALIZE-DRIFT", "je-finalize-drift");
        var targetDate = new DateOnly(2026, 1, 1);
        var created = await scope.Services.GetRequiredService<CreateDraftJournalEntryHandler>()
            .HandleAsync(Request(setup, BalanceEffect.Financial, 100m, 100m, targetDate));
        await scope.Services.GetRequiredService<PostJournalEntryHandler>()
            .HandleAsync(setup.FiscalYearId, created.Id);
        await using (var connection = CreateShardConnection())
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                "UPDATE daily_account_turnover SET debit_turnover = debit_turnover + 1 WHERE fiscal_year_id = @FiscalYearId AND balance_date = @TargetDate",
                new { setup.FiscalYearId, TargetDate = targetDate });
        }

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            scope.Services.GetRequiredService<FinalizeFiscalYearHandler>().HandleAsync(
                setup.FiscalYearId,
                new FinalizeFiscalYearRequest { FinalizedThroughDate = targetDate }));

        Assert.Equal(JournalEntryErrors.ProjectionReconciliationFailed, exception.ErrorCode);
        var entry = await scope.Services.GetRequiredService<GetJournalEntryHandler>()
            .GetByIdAsync(setup.FiscalYearId, created.Id);
        Assert.False(entry.NumberFinalized);
        var fiscalYear = await scope.Services.GetRequiredService<GetFiscalYearHandler>()
            .HandleAsync(setup.FiscalYearId);
        Assert.Equal(targetDate.AddDays(-1), fiscalYear.FinalizedThroughDate);
    }

    [Fact]
    public async Task Finalize_WithSkippedDate_IsRejected()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var setup = await SetupAsync(scope, "JE-FINALIZE-SKIP", "je-finalize-skip");

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            scope.Services.GetRequiredService<FinalizeFiscalYearHandler>().HandleAsync(
                setup.FiscalYearId,
                new FinalizeFiscalYearRequest
                {
                    FinalizedThroughDate = new DateOnly(2026, 1, 2)
                }));

        Assert.Equal(JournalEntryErrors.InvalidFinalizationDate, exception.ErrorCode);
    }

    private async Task<JournalEntryDetailResponse> CreateAsync(
        ServiceScopeHandle scope, AccountSetup setup, BalanceEffect balanceEffect) =>
        await scope.Services.GetRequiredService<CreateDraftJournalEntryHandler>()
            .HandleAsync(Request(setup, balanceEffect, 100m, 100m));

    private static CreateDraftJournalEntryRequest Request(
        AccountSetup setup, BalanceEffect balanceEffect, decimal debitAmount, decimal creditAmount,
        DateOnly? accountingDate = null) => new()
        {
            AccountingBookId = setup.BookId,
            FiscalYearId = setup.FiscalYearId,
            AccountingDate = accountingDate ?? AccountingDate,
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
