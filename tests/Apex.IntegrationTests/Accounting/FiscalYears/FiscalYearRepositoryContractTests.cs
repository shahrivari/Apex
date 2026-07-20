using Apex.Application.Abstractions.Data;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.IntegrationTests.Accounting.FiscalYears;

[Collection(ApexIntegrationTestCollection.Name)]
public sealed class FiscalYearRepositoryContractTests(ApexIntegrationTestFixture fixture)
    : ApexIntegrationTestBase(fixture)
{
    private static readonly DateTime CreatedAt = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task InsertAndUpdate_ShouldRoundTripOnFiscalYearShard()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var bookId = await CreateBookAsync(scope);
        var repository = scope.Services.GetRequiredService<IFiscalYearWriteRepository>();
        var connectionFactory = scope.Services.GetRequiredService<IShardConnectionFactory>();
        var shardKey = scope.Services.GetRequiredService<IShardKeyFactory<long>>().Create(9_000_000_000_001);
        await scope.Services.GetRequiredService<IShardAssignmentProvisioner>().EnsureAssignedAsync(shardKey);
        var first = FiscalYear.Create(9_000_000_000_001, bookId, "First", new DateOnly(2025, 1, 1),
            new DateOnly(2025, 12, 31), CreatedAt);
        await using (var shard = await connectionFactory.OpenAsync(shardKey, beginTransaction: true))
        {
            await repository.InsertAsync(shard, first);
            await shard.Transaction!.CommitAsync();
        }
        first.Open(CreatedAt.AddHours(1));
        await using (var shard = await connectionFactory.OpenAsync(shardKey, beginTransaction: true))
        {
            await repository.UpdateAsync(shard, first);
            await shard.Transaction!.CommitAsync();
        }

        var row = await scope.Services.GetRequiredService<IFiscalYearReadRepository>().GetByIdAsync(first.Id);
        Assert.NotNull(row);
        Assert.Equal("OPEN", row.Status);
        Assert.Equal(1, row.NextReferenceNumber);
        Assert.Equal(1, row.NextJournalEntryNumber);
    }

    private static async Task<long> CreateBookAsync(ServiceScopeHandle scope)
    {
        var result = await scope.Services.GetRequiredService<CreateAccountingBookHandler>().HandleAsync(
            new CreateAccountingBookRequest
            {
                Code = "FY-REPOSITORY",
                Title = "Fiscal year repository contract",
                OwnerType = "TEST",
                OwnerId = "fy-repository"
            });
        return result.Id;
    }
}
