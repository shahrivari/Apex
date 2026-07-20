using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Ids;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ActivateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;
using Apex.Modules.Accounting.FiscalYears.UseCases.CreateFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.OpenFiscalYear;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.IntegrationTests.Accounting.JournalEntries;

[Collection(ApexIntegrationTestCollection.Name)]
public sealed class JournalEntryRepositoryContractTests(ApexIntegrationTestFixture fixture)
    : ApexIntegrationTestBase(fixture)
{
    private static readonly DateTime RegisteredAt = new(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly AccountingDate = new(2026, 6, 1);

    [Fact]
    public async Task Insert_Then_Read_RoundTripsHeaderAndLines()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync(scope, "JE-REPO-RT", "je-repo-rt");
        var context = await ShardContextAsync(scope, fiscalYearId);

        long entryId;
        const long journalEntryNumber = 1;
        await using (var shard = await context.OpenTransactionAsync())
        {
            var entry = JournalEntry.Create(
                context.IdGenerator.NewId(), bookId, fiscalYearId, referenceNumber: 7, journalEntryNumber,
                AccountingDate, RegisteredAt, "Round trip", DocumentType.General, InsertionType.Migration,
                BalanceEffect.Financial, "MIGRATION", "SRC-1",
                [
                    new JournalEntryLineInput(
                        context.IdGenerator.NewId(), JournalEntrySide.Debit, 150.25m, "1", "01", "01", null, "debit"),
                    new JournalEntryLineInput(
                        context.IdGenerator.NewId(), JournalEntrySide.Credit, 150.25m, "2", "02", "02", "D-100", "credit")
                ], RegisteredAt);
            entryId = entry.Id;
            await context.WriteRepository.InsertAsync(shard, entry);
            await shard.Transaction!.CommitAsync();
        }

        var model = await context.ReadRepository.GetByIdAsync(fiscalYearId, entryId);

        Assert.NotNull(model);
        Assert.Equal(bookId, model.Header.AccountingBookId);
        Assert.Equal(fiscalYearId, model.Header.FiscalYearId);
        Assert.Equal(7, model.Header.ReferenceNumber);
        Assert.Equal(1, journalEntryNumber);
        Assert.Equal(AccountingDate, model.Header.AccountingDate);
        Assert.Equal("DRAFT", model.Header.Status);
        Assert.Equal("GENERAL", model.Header.DocumentType);
        Assert.Equal("MIGRATION", model.Header.InsertionType);
        Assert.Equal("FINANCIAL", model.Header.BalanceEffect);
        Assert.Equal("MIGRATION", model.Header.SourceType);
        Assert.Equal("SRC-1", model.Header.SourceReference);
        Assert.False(model.Header.NumberFinalized);
        Assert.Null(model.Header.PostedAt);
        Assert.Null(model.Header.UpdatedAt);
        Assert.Equal(2, model.Lines.Count);
        Assert.Equal([1, 2], model.Lines.Select(l => l.RowNumber).ToArray());
        Assert.Equal(150.25m, model.Lines[0].Amount);
        Assert.Equal("DEBIT", model.Lines[0].Side);
        Assert.Null(model.Lines[0].DetailAccountCode);
        Assert.Equal("D-100", model.Lines[1].DetailAccountCode);
    }

    [Fact]
    public async Task GetForUpdate_RehydratesDomainEntity()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync(scope, "JE-REPO-REHYDRATE", "je-repo-rehydrate");
        var context = await ShardContextAsync(scope, fiscalYearId);

        long entryId;
        await using (var shard = await context.OpenTransactionAsync())
        {
            const long number = 1;
            var entry = Entry(context, bookId, fiscalYearId, referenceNumber: 3, number, BalanceEffect.Statistical);
            entryId = entry.Id;
            await context.WriteRepository.InsertAsync(shard, entry);
            await shard.Transaction!.CommitAsync();
        }

        await using var readShard = await context.OpenTransactionAsync();
        var rehydrated = await context.WriteRepository.GetForUpdateAsync(readShard, fiscalYearId, entryId);

        Assert.NotNull(rehydrated);
        Assert.Equal(JournalEntryStatus.Draft, rehydrated.Status);
        Assert.Equal(BalanceEffect.Statistical, rehydrated.BalanceEffect);
        Assert.Equal(2, rehydrated.Lines.Count);
        Assert.Equal(entryId, rehydrated.Id);
    }

    [Fact]
    public async Task DuplicateReferenceNumber_ViolatesUniqueConstraint()
    {
        await ResetDatabasesAsync();
        await using var scope = await CreateScopeAsync();
        var (bookId, fiscalYearId) = await CreateOpenFiscalYearAsync(scope, "JE-REPO-UNIQUE", "je-repo-unique");
        var context = await ShardContextAsync(scope, fiscalYearId);

        await using var shard = await context.OpenTransactionAsync();
        await context.WriteRepository.InsertAsync(
            shard, Entry(context, bookId, fiscalYearId, referenceNumber: 5, number: 1, BalanceEffect.Financial));

        await Assert.ThrowsAsync<SqlException>(() => context.WriteRepository.InsertAsync(
            shard, Entry(context, bookId, fiscalYearId, referenceNumber: 5, number: 2, BalanceEffect.Financial)));
    }

    private static JournalEntry Entry(
        ShardTestContext context, long bookId, long fiscalYearId, long referenceNumber, long number,
        BalanceEffect balanceEffect) =>
        JournalEntry.Create(
            context.IdGenerator.NewId(), bookId, fiscalYearId, referenceNumber, number, AccountingDate, RegisteredAt,
            "entry", DocumentType.General, InsertionType.Manual, balanceEffect, null, null,
            [
                new JournalEntryLineInput(context.IdGenerator.NewId(), JournalEntrySide.Debit, 5m, "1", "01", "01", null, "d"),
                new JournalEntryLineInput(context.IdGenerator.NewId(), JournalEntrySide.Credit, 5m, "1", "01", "01", null, "c")
            ], RegisteredAt);

    private async Task ResetDatabasesAsync()
    {
        await ResetAccountingDatabaseAsync();
        await ResetShardDatabaseAsync();
    }

    private static async Task<ShardTestContext> ShardContextAsync(ServiceScopeHandle scope, long fiscalYearId)
    {
        var shardKey = scope.Services.GetRequiredService<IShardKeyFactory<long>>().Create(fiscalYearId);
        await scope.Services.GetRequiredService<IShardAssignmentProvisioner>().EnsureAssignedAsync(shardKey);
        return new ShardTestContext(
            scope.Services.GetRequiredService<IShardConnectionFactory>(),
            shardKey,
            scope.Services.GetRequiredService<IJournalEntryWriteRepository>(),
            scope.Services.GetRequiredService<IJournalEntryReadRepository>(),
            scope.Services.GetRequiredService<IIdGenerator>());
    }

    private sealed record ShardTestContext(
        IShardConnectionFactory ConnectionFactory,
        ShardKey ShardKey,
        IJournalEntryWriteRepository WriteRepository,
        IJournalEntryReadRepository ReadRepository,
        IIdGenerator IdGenerator)
    {
        public Task<IShardConnection> OpenTransactionAsync() =>
            ConnectionFactory.OpenAsync(ShardKey, beginTransaction: true);
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
}
