using Apex.Application.Abstractions.Data;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.DetailAccounts.Domain;
using Apex.Modules.Accounting.DetailAccounts.Repositories;
using Apex.Modules.Accounting.DetailAccounts.Repositories.Rows;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.IntegrationTests.Accounting.DetailAccounts;

[Collection(ApexIntegrationTestCollection.Name)]
public sealed class DetailAccountRepositoryContractTests(ApexIntegrationTestFixture fixture)
    : ApexIntegrationTestBase(fixture)
{
    [Fact]
    public async Task InsertReadListAndPostingSearch_ShouldRoundTripCompleteRows()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var write = scope.Services.GetRequiredService<IDetailAccountWriteRepository>();
        var read = scope.Services.GetRequiredService<IDetailAccountReadRepository>();
        var createdAt = new DateTime(2026, 7, 1, 10, 20, 30, DateTimeKind.Utc);
        var person = DetailAccount.Create(
            1001,
            " person-01 ",
            "Person One",
            DetailAccountType.Person,
            createdAt
        );
        var bank = DetailAccount.Create(
            1002,
            "bank-01",
            "Bank One",
            DetailAccountType.Bank,
            createdAt.AddMinutes(1)
        );

        await write.InsertAsync(person);
        await write.InsertAsync(bank);

        var byId = Assert.IsType<DetailAccountRow>(await read.GetByIdAsync(person.Id));
        Assert.Equal(
            (person.Id, "PERSON-01", "Person One", "PERSON", "ACTIVE"),
            (byId.Id, byId.Code, byId.Name, byId.Type, byId.Status)
        );
        Assert.Equal(createdAt, byId.CreatedAt);
        Assert.Null(byId.UpdatedAt);
        Assert.Null(byId.ArchivedAt);
        Assert.Equal(person.Id, (await read.GetByCodeAsync("PERSON-01"))?.Id);

        var listed = await read.ListAsync("PERSON", "ACTIVE", "person", 1, 10);
        Assert.Equal(1, listed.TotalCount);
        Assert.Equal(person.Id, Assert.Single(listed.Items).Id);

        var posting = await read.SearchForPostingAsync("PERSON", "one", 10);
        Assert.Equal(person.Id, Assert.Single(posting).Id);
        Assert.Empty(await read.SearchForPostingAsync("BANK", "person", 10));
    }

    [Fact]
    public async Task UpdateAndLockedRead_ShouldRehydrateEveryDomainProperty()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var write = scope.Services.GetRequiredService<IDetailAccountWriteRepository>();
        var read = scope.Services.GetRequiredService<IDetailAccountReadRepository>();
        var tx = scope.Services.GetRequiredService<IGeneralTransactionRunner>();
        var createdAt = new DateTime(2026, 7, 2, 1, 2, 3, DateTimeKind.Utc);
        var updatedAt = createdAt.AddDays(1);
        var account = DetailAccount.Create(
            2001,
            "SYMBOL-01",
            "Old",
            DetailAccountType.Symbol,
            createdAt
        );
        await write.InsertAsync(account);

        await tx.ExecuteAsync(async ct =>
        {
            var loaded = Assert.IsType<DetailAccount>(
                await write.GetForUpdateAsync(account.Id, ct)
            );
            loaded.Update("New Name", DetailAccountType.Bank, updatedAt);
            loaded.Archive(updatedAt.AddHours(1));
            await write.UpdateAsync(loaded, ct);
        });

        var domain = await tx.ExecuteAsync(async ct =>
            Assert.IsType<DetailAccount>(await write.GetForUpdateAsync(account.Id, ct))
        );
        Assert.Equal((2001L, "SYMBOL-01", "New Name"), (domain.Id, domain.Code, domain.Name));
        Assert.Equal(DetailAccountType.Bank, domain.Type);
        Assert.Equal(DetailAccountStatus.Archived, domain.Status);
        Assert.Equal(createdAt, domain.CreatedAt);
        Assert.Equal(updatedAt.AddHours(1), domain.UpdatedAt);
        Assert.Equal(updatedAt.AddHours(1), domain.ArchivedAt);
        Assert.Empty(await read.SearchForPostingAsync("BANK", null, 10));
    }

    [Fact]
    public async Task WriteRepository_ShouldParticipateInRollback()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var write = scope.Services.GetRequiredService<IDetailAccountWriteRepository>();
        var read = scope.Services.GetRequiredService<IDetailAccountReadRepository>();
        var tx = scope.Services.GetRequiredService<IGeneralTransactionRunner>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tx.ExecuteAsync(async ct =>
            {
                await write.InsertAsync(
                    DetailAccount.Create(
                        3001,
                        "ROLLBACK",
                        "Rollback",
                        DetailAccountType.Person,
                        DateTime.UtcNow
                    ),
                    ct
                );
                throw new InvalidOperationException("force rollback");
            })
        );

        Assert.Null(await read.GetByIdAsync(3001));
    }

    [Fact]
    public async Task Database_ShouldEnforceUniqueCodeRequiredValuesAndEnums()
    {
        await ResetAccountingDatabaseAsync();
        await using var connection = CreateAccountingConnection();
        await connection.OpenAsync();
        const string sql =
            "INSERT INTO detail_account(id,code,name,type,status,created_at) VALUES(@Id,@Code,@Name,@Type,@Status,@CreatedAt)";
        var now = DateTime.UtcNow;
        await connection.ExecuteAsync(
            sql,
            new
            {
                Id = 4001L,
                Code = "UNIQUE",
                Name = "One",
                Type = "PERSON",
                Status = "ACTIVE",
                CreatedAt = now,
            }
        );

        await Assert.ThrowsAsync<SqlException>(() =>
            connection.ExecuteAsync(
                sql,
                new
                {
                    Id = 4002L,
                    Code = "UNIQUE",
                    Name = "Two",
                    Type = "BANK",
                    Status = "ACTIVE",
                    CreatedAt = now,
                }
            )
        );
        await Assert.ThrowsAsync<SqlException>(() =>
            connection.ExecuteAsync(
                sql,
                new
                {
                    Id = 4003L,
                    Code = "BAD-TYPE",
                    Name = "Bad",
                    Type = "OTHER",
                    Status = "ACTIVE",
                    CreatedAt = now,
                }
            )
        );
        await Assert.ThrowsAsync<SqlException>(() =>
            connection.ExecuteAsync(
                sql,
                new
                {
                    Id = 4004L,
                    Code = "BAD-STATUS",
                    Name = "Bad",
                    Type = "BANK",
                    Status = "REMOVED",
                    CreatedAt = now,
                }
            )
        );
    }

    [Fact]
    public async Task Delete_ShouldRemoveRowAndPermanentlyReserveCode()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var write = scope.Services.GetRequiredService<IDetailAccountWriteRepository>();
        var read = scope.Services.GetRequiredService<IDetailAccountReadRepository>();
        var account = DetailAccount.Create(
            5001,
            "RETIRED",
            "Retired",
            DetailAccountType.Person,
            DateTime.UtcNow
        );
        await write.InsertAsync(account);
        account.Update(account.Name, account.Type, DateTime.UtcNow.AddMinutes(1));
        await write.DeleteAsync(account);

        Assert.Null(await read.GetByIdAsync(account.Id));
        Assert.True(await write.CodeExistsAsync(account.Code));
    }
}
