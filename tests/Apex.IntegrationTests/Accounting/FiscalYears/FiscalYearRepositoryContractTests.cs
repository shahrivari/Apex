using Apex.Application.Abstractions.Data;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.IntegrationTests.Accounting.FiscalYears;

[Collection(ApexIntegrationTestCollection.Name)]
public sealed class FiscalYearRepositoryContractTests(ApexIntegrationTestFixture fixture)
    : ApexIntegrationTestBase(fixture)
{
    private static readonly DateTime CreatedAt = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Database_ShouldEnforceOnlyOneOpenFiscalYearPerBook()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var bookId = await CreateBookAsync(scope);
        var repository = scope.Services.GetRequiredService<IFiscalYearWriteRepository>();
        var transactionRunner = scope.Services.GetRequiredService<IGeneralTransactionRunner>();
        var first = FiscalYear.Create(9_000_000_000_001, bookId, "First", new DateOnly(2025, 1, 1),
            new DateOnly(2025, 12, 31), CreatedAt);
        var second = FiscalYear.Create(9_000_000_000_002, bookId, "Second", new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31), CreatedAt);

        await transactionRunner.ExecuteAsync(async ct =>
        {
            await repository.InsertAsync(first, ct);
            await repository.InsertAsync(second, ct);
        });
        first.Open(CreatedAt.AddHours(1));
        second.Open(CreatedAt.AddHours(1));
        await transactionRunner.ExecuteAsync(ct => repository.UpdateAsync(first, ct));

        await Assert.ThrowsAsync<SqlException>(() =>
            transactionRunner.ExecuteAsync(ct => repository.UpdateAsync(second, ct)));
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
