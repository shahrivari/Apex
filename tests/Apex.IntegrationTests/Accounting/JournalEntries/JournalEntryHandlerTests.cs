using Apex.Application.Abstractions.Exceptions;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ActivateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.UseCases.CreateFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.OpenFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.GetFiscalYear;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using Apex.Modules.Accounting.JournalEntries.UseCases.AppendDraftLines;
using Apex.Modules.Accounting.JournalEntries.UseCases.CreateDraftJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.DeleteDraftJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.GetJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.ReplaceDraftLines;
using Apex.Modules.Accounting.JournalEntries.UseCases.SearchJournalEntries;
using Apex.Modules.Accounting.JournalEntries.UseCases.UpdateDraftJournalEntry;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.IntegrationTests.Accounting.JournalEntries;

[Collection(ApexIntegrationTestCollection.Name)]
public sealed class JournalEntryHandlerTests(ApexIntegrationTestFixture fixture)
    : ApexIntegrationTestBase(fixture)
{
    private static readonly DateOnly AccountingDate = new(2026, 6, 1);

    [Fact]
    public async Task CreateDraft_AllocatesNumbersAndPersistsToShard()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync(scope, "JE-CREATE", "je-create");

        var first = await Create(scope).HandleAsync(DraftRequest(bookId, fiscalYearId));
        var second = await Create(scope).HandleAsync(DraftRequest(bookId, fiscalYearId));

        Assert.Equal(fiscalYearId, first.FiscalYearId);
        Assert.Equal("DRAFT", first.Status);
        Assert.Equal(1, first.ReferenceNumber);
        Assert.Equal(1, first.JournalEntryNumber);
        Assert.Equal([1, 2], first.Lines.Select(l => l.RowNumber).ToArray());
        Assert.Equal(2, second.ReferenceNumber);
        Assert.Equal(2, second.JournalEntryNumber);

        var fetched = await scope.Services.GetRequiredService<GetJournalEntryHandler>()
            .GetByIdAsync(fiscalYearId, first.Id);
        Assert.Equal(first.Id, fetched.Id);
        Assert.Equal(2, fetched.Lines.Count);
    }

    [Fact]
    public async Task CreateDraft_OnUnopenedFiscalYear_IsRejected()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var bookId = await CreateActiveBookAsync(scope, "JE-DRAFT-FY", "je-draft-fy");
        var fiscalYear = await scope.Services.GetRequiredService<CreateFiscalYearHandler>().HandleAsync(new CreateFiscalYearRequest
        {
            AccountingBookId = bookId,
            Title = "2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31)
        });

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Create(scope).HandleAsync(DraftRequest(bookId, fiscalYear.Id)));

        Assert.Equal(JournalEntryErrors.FiscalYearNotOpen, exception.ErrorCode);
    }

    [Fact]
    public async Task UpdateDraft_ChangesHeaderFields()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync(scope, "JE-UPDATE", "je-update");
        var created = await Create(scope).HandleAsync(DraftRequest(bookId, fiscalYearId));

        var updated = await scope.Services.GetRequiredService<UpdateDraftJournalEntryHandler>().HandleAsync(
            fiscalYearId, created.Id,
            new UpdateDraftJournalEntryRequest
            {
                AccountingDate = new DateOnly(2026, 7, 15),
                Description = "Revised description",
                DocumentType = "OPENING",
                BalanceEffect = "STATISTICAL"
            });

        Assert.Equal(new DateOnly(2026, 7, 15), updated.AccountingDate);
        Assert.Equal("Revised description", updated.Description);
        Assert.Equal("OPENING", updated.DocumentType);
        Assert.Equal("STATISTICAL", updated.BalanceEffect);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task AppendThenReplaceLines_UpdatesDraftLines()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync(scope, "JE-LINES", "je-lines");
        var created = await Create(scope).HandleAsync(DraftRequest(bookId, fiscalYearId));

        var appended = await scope.Services.GetRequiredService<AppendDraftLinesHandler>().HandleAsync(
            fiscalYearId, created.Id,
            new AppendDraftLinesRequest { Lines = [Line("DEBIT", 25m), Line("CREDIT", 25m)] });
        Assert.Equal(4, appended.Lines.Count);
        Assert.Equal([1, 2, 3, 4], appended.Lines.Select(l => l.RowNumber).ToArray());

        var replaced = await scope.Services.GetRequiredService<ReplaceDraftLinesHandler>().HandleAsync(
            fiscalYearId, created.Id,
            new ReplaceDraftLinesRequest { Lines = [Line("DEBIT", 10m), Line("CREDIT", 10m)] });
        Assert.Equal(2, replaced.Lines.Count);
        Assert.Equal([1, 2], replaced.Lines.Select(l => l.RowNumber).ToArray());
    }

    [Fact]
    public async Task DeleteDraft_RemovesEntry()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync(scope, "JE-DELETE", "je-delete");
        var created = await Create(scope).HandleAsync(DraftRequest(bookId, fiscalYearId));

        await scope.Services.GetRequiredService<DeleteDraftJournalEntryHandler>()
            .HandleAsync(fiscalYearId, created.Id);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Services.GetRequiredService<GetJournalEntryHandler>().GetByIdAsync(fiscalYearId, created.Id));
        Assert.Equal(JournalEntryErrors.NotFound, exception.ErrorCode);
    }

    [Fact]
    public async Task Search_ReturnsEntriesForFiscalYear()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync(scope, "JE-SEARCH", "je-search");
        await Create(scope).HandleAsync(DraftRequest(bookId, fiscalYearId));
        await Create(scope).HandleAsync(DraftRequest(bookId, fiscalYearId));

        var result = await scope.Services.GetRequiredService<SearchJournalEntriesHandler>().HandleAsync(
            new SearchJournalEntriesRequest { FiscalYearId = fiscalYearId, Status = "DRAFT", Page = 1, PageSize = 10 });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal(fiscalYearId, item.FiscalYearId));
    }

    [Fact]
    public async Task CreateWithSourceReference_Replay_ReturnsExistingEntry()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync(scope, "JE-IDEMPOTENT", "je-idempotent");

        var first = await Create(scope).HandleAsync(SourcedRequest(bookId, fiscalYearId, "SRC-100", "First"));
        var replay = await Create(scope).HandleAsync(SourcedRequest(bookId, fiscalYearId, "SRC-100", "First"));

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(first.ReferenceNumber, replay.ReferenceNumber);
    }

    [Fact]
    public async Task CreateWithSourceReference_DivergentPayload_Conflicts()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync(scope, "JE-IDEMPOTENT-CONFLICT", "je-conflict");
        await Create(scope).HandleAsync(SourcedRequest(bookId, fiscalYearId, "SRC-200", "Original"));

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            Create(scope).HandleAsync(SourcedRequest(bookId, fiscalYearId, "SRC-200", "Changed description")));

        Assert.Equal(JournalEntryErrors.ConflictingIdempotentRequest, exception.ErrorCode);
    }

    [Fact]
    public async Task WrongFiscalYearRoute_CannotReadOrMutateEntryOnSharedPhysicalShard()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var (firstBookId, firstFiscalYearId) = await CreateOpenFiscalYearAsync(
            scope, "JE-PARTITION-1", "je-partition-1");
        var (secondBookId, secondFiscalYearId) = await CreateOpenFiscalYearAsync(
            scope, "JE-PARTITION-2", "je-partition-2");
        var created = await Create(scope).HandleAsync(DraftRequest(firstBookId, firstFiscalYearId));
        await Create(scope).HandleAsync(DraftRequest(secondBookId, secondFiscalYearId));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Services.GetRequiredService<GetJournalEntryHandler>()
                .GetByIdAsync(secondFiscalYearId, created.Id));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Services.GetRequiredService<AppendDraftLinesHandler>().HandleAsync(
                secondFiscalYearId, created.Id,
                new AppendDraftLinesRequest { Lines = [Line("DEBIT", 1m)] }));

        var unchanged = await scope.Services.GetRequiredService<GetJournalEntryHandler>()
            .GetByIdAsync(firstFiscalYearId, created.Id);
        Assert.Equal(2, unchanged.Lines.Count);
    }

    [Fact]
    public async Task CreateDraft_OnFinalizedDate_IsRejected()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync(
            scope, "JE-FINALIZED-LINES", "je-finalized-lines");
        await scope.Services.GetRequiredService<FinalizeFiscalYearHandler>().HandleAsync(
            fiscalYearId, new FinalizeFiscalYearRequest { FinalizedThroughDate = AccountingDate });

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Create(scope).HandleAsync(DraftRequest(bookId, fiscalYearId)));

        Assert.Equal(JournalEntryErrors.AccountingDateFinalized, exception.ErrorCode);
    }

    [Fact]
    public async Task ConcurrentCreateAndFinalization_CommitOnlyOneValidOutcome()
    {
        await ResetDatabasesAsync();
        long bookId;
        long fiscalYearId;
        await using (var setupScope = await CreateScopeAsync())
        {
            (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync(
                setupScope, "JE-FINALIZE-RACE", "je-finalize-race");
        }

        var createAttempt = Task.Run(async () =>
        {
            await using var scope = await CreateScopeAsync();
            try
            {
                await Create(scope).HandleAsync(DraftRequest(bookId, fiscalYearId));
                return "create_success";
            }
            catch (BusinessRuleException)
            {
                return "create_failed";
            }
        });
        var finalizeAttempt = Task.Run(async () =>
        {
            await using var scope = await CreateScopeAsync();
            try
            {
                await scope.Services.GetRequiredService<FinalizeFiscalYearHandler>().HandleAsync(
                    fiscalYearId,
                    new FinalizeFiscalYearRequest { FinalizedThroughDate = AccountingDate });
                return "finalize_success";
            }
            catch (ConflictException)
            {
                return "finalize_failed";
            }
        });

        var outcomes = await Task.WhenAll(createAttempt, finalizeAttempt);
        Assert.Single(outcomes, outcome => outcome.EndsWith("success", StringComparison.Ordinal));

        await using var verificationScope = await CreateScopeAsync();
        var fiscalYear = await verificationScope.Services.GetRequiredService<GetFiscalYearHandler>()
            .HandleAsync(fiscalYearId);
        var entries = await verificationScope.Services.GetRequiredService<SearchJournalEntriesHandler>()
            .HandleAsync(new SearchJournalEntriesRequest
            {
                FiscalYearId = fiscalYearId,
                Page = 1,
                PageSize = 10
            });
        if (outcomes.Contains("finalize_success", StringComparer.Ordinal))
        {
            Assert.Equal(AccountingDate, fiscalYear.FinalizedThroughDate);
            Assert.Empty(entries.Items);
        }
        else
        {
            Assert.Equal(AccountingDate.AddDays(-1), fiscalYear.FinalizedThroughDate);
            Assert.Single(entries.Items);
        }
    }

    private static CreateDraftJournalEntryRequest SourcedRequest(
        long bookId, long fiscalYearId, string sourceReference, string description) => new()
        {
            AccountingBookId = bookId,
            FiscalYearId = fiscalYearId,
            AccountingDate = AccountingDate,
            Description = description,
            DocumentType = "GENERAL",
            InsertionType = "SYSTEM",
            BalanceEffect = "FINANCIAL",
            SourceType = "MIGRATION",
            SourceReference = sourceReference,
            Lines = [Line("DEBIT", 100m), Line("CREDIT", 100m)]
        };

    private static CreateDraftJournalEntryHandler Create(ServiceScopeHandle scope) =>
        scope.Services.GetRequiredService<CreateDraftJournalEntryHandler>();

    private static CreateDraftJournalEntryRequest DraftRequest(long bookId, long fiscalYearId) => new()
    {
        AccountingBookId = bookId,
        FiscalYearId = fiscalYearId,
        AccountingDate = AccountingDate,
        Description = "Journal entry",
        DocumentType = "GENERAL",
        InsertionType = "MANUAL",
        BalanceEffect = "FINANCIAL",
        Lines = [Line("DEBIT", 100m), Line("CREDIT", 100m)]
    };

    private static JournalEntryLineRequest Line(string side, decimal amount) => new()
    {
        Side = side,
        Amount = amount,
        AccountClassCode = "1",
        GeneralAccountCode = "01",
        SubsidiaryAccountCode = "01",
        DetailAccountCode = "D-1",
        Description = "line"
    };

    private async Task ResetDatabasesAsync()
    {
        await ResetAccountingDatabaseAsync();
        await ResetShardDatabaseAsync();
    }

    private static async Task<long> CreateActiveBookAsync(ServiceScopeHandle scope, string code, string ownerId)
    {
        var book = await scope.Services.GetRequiredService<CreateAccountingBookHandler>().HandleAsync(
            new CreateAccountingBookRequest { Code = code, Title = code, OwnerType = "TEST", OwnerId = ownerId });
        await scope.Services.GetRequiredService<ActivateAccountingBookHandler>().HandleAsync(book.Id);
        return book.Id;
    }

    private async Task<(long BookId, long FiscalYearId)> CreateOpenFiscalYearAsync(
        ServiceScopeHandle scope, string code, string ownerId)
    {
        var bookId = await CreateActiveBookAsync(scope, code, ownerId);
        var fiscalYear = await scope.Services.GetRequiredService<CreateFiscalYearHandler>().HandleAsync(
            new CreateFiscalYearRequest
            {
                AccountingBookId = bookId,
                Title = "2026",
                StartDate = AccountingDate,
                EndDate = new DateOnly(2026, 12, 31)
            });
        await scope.Services.GetRequiredService<OpenFiscalYearHandler>().HandleAsync(fiscalYear.Id);
        return (bookId, fiscalYear.Id);
    }
}
