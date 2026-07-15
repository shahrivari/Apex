namespace Apex.IntegrationTests.Accounting.AccountingBooks;

using Apex.Application.Abstractions.Data;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.AccountingBooks.Domain;
using Apex.Modules.Accounting.AccountingBooks.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

[Collection(ApexIntegrationTestCollection.Name)]
public sealed class AccountingBookRepositoryContractTests(ApexIntegrationTestFixture fixture)
    : ApexIntegrationTestBase(fixture)
{
    [Fact]
    public async Task Insert_Then_Read_Should_RoundTrip_Complete_Row_And_All_Read_Operations()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var writeRepository = scope.Services.GetRequiredService<IAccountingBookWriteRepository>();
        var readRepository = scope.Services.GetRequiredService<IAccountingBookReadRepository>();
        var createdAt = Utc(2026, 2, 3, 4, 5, 6, 123);
        var book = AccountingBook.Create(
            1_000_000_000_001,
            "BOOK-ROUNDTRIP",
            "Round-trip book",
            "PORTFOLIO",
            "OWNER-42",
            createdAt);

        await writeRepository.InsertAsync(book);

        var byId = await readRepository.GetByIdAsync(book.Id);
        Assert.NotNull(byId);
        Assert.Equal(book.Id, byId.Id);
        Assert.Equal("BOOK-ROUNDTRIP", byId.Code);
        Assert.Equal("Round-trip book", byId.Title);
        Assert.Equal("PORTFOLIO", byId.OwnerType);
        Assert.Equal("OWNER-42", byId.OwnerId);
        Assert.Equal("DRAFT", byId.Status);
        Assert.Equal(createdAt, byId.CreatedAt);
        Assert.Null(byId.UpdatedAt);
        Assert.Null(byId.ActivatedAt);
        Assert.Null(byId.SuspendedAt);
        Assert.Null(byId.ArchivedAt);

        Assert.Equal(book.Id, (await readRepository.GetByCodeAsync(book.Code))?.Id);
        Assert.Equal(book.Id, (await readRepository.GetByOwnerAsync(book.OwnerType, book.OwnerId))?.Id);
        Assert.True(await readRepository.ExistsByCodeAsync(book.Code));
        Assert.True(await readRepository.ExistsByOwnerAsync(book.OwnerType, book.OwnerId));

        var (items, totalCount) = await readRepository.ListAsync(
            status: "DRAFT",
            ownerType: book.OwnerType,
            ownerId: book.OwnerId,
            search: "Round-trip");
        Assert.Equal(1, totalCount);
        var listed = Assert.Single(items);
        Assert.Equal(byId.Id, listed.Id);
        Assert.Equal(byId.Code, listed.Code);
        Assert.Equal(byId.Title, listed.Title);
        Assert.Equal(byId.OwnerType, listed.OwnerType);
        Assert.Equal(byId.OwnerId, listed.OwnerId);
        Assert.Equal(byId.Status, listed.Status);
        Assert.Equal(byId.CreatedAt, listed.CreatedAt);
        Assert.Equal(byId.UpdatedAt, listed.UpdatedAt);
        Assert.Equal(byId.ActivatedAt, listed.ActivatedAt);
        Assert.Equal(byId.SuspendedAt, listed.SuspendedAt);
        Assert.Equal(byId.ArchivedAt, listed.ArchivedAt);
    }

    [Fact]
    public async Task Update_Then_LockedRead_Should_RoundTrip_Statuses_And_Rehydrate_Domain()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var writeRepository = scope.Services.GetRequiredService<IAccountingBookWriteRepository>();
        var readRepository = scope.Services.GetRequiredService<IAccountingBookReadRepository>();
        var transactionRunner = scope.Services.GetRequiredService<IGeneralTransactionRunner>();
        var createdAt = Utc(2026, 3, 1, 8, 0, 0, 100);
        var activatedAt = Utc(2026, 3, 2, 9, 10, 11, 200);
        var suspendedAt = Utc(2026, 3, 3, 10, 20, 21, 300);
        var archivedAt = Utc(2026, 3, 4, 11, 30, 31, 400);
        var book = AccountingBook.Create(
            1_000_000_000_002,
            "BOOK-LIFECYCLE",
            "Lifecycle book",
            "FUND",
            "OWNER-84",
            createdAt);

        await writeRepository.InsertAsync(book);

        await transactionRunner.ExecuteAsync(async ct =>
        {
            var domain = Assert.IsType<AccountingBook>(
                await writeRepository.GetByIdForUpdateAsync(book.Id, ct));
            domain.Activate(activatedAt);
            await writeRepository.UpdateStatusAsync(domain, ct);
        });
        Assert.Equal("ACTIVE", (await readRepository.GetByIdAsync(book.Id))?.Status);

        await transactionRunner.ExecuteAsync(async ct =>
        {
            var domain = Assert.IsType<AccountingBook>(
                await writeRepository.GetByIdForUpdateAsync(book.Id, ct));
            domain.Suspend(suspendedAt);
            await writeRepository.UpdateStatusAsync(domain, ct);
        });

        var rehydrated = await transactionRunner.ExecuteAsync(async ct =>
            Assert.IsType<AccountingBook>(
                await writeRepository.GetByIdForUpdateAsync(book.Id, ct)));
        Assert.Equal(book.Id, rehydrated.Id);
        Assert.Equal(book.Code, rehydrated.Code);
        Assert.Equal(book.Title, rehydrated.Title);
        Assert.Equal(book.OwnerType, rehydrated.OwnerType);
        Assert.Equal(book.OwnerId, rehydrated.OwnerId);
        Assert.Equal(AccountingBookStatus.Suspended, rehydrated.Status);
        Assert.Equal(createdAt, rehydrated.CreatedAt);
        Assert.Equal(suspendedAt, rehydrated.UpdatedAt);
        Assert.Equal(activatedAt, rehydrated.ActivatedAt);
        Assert.Equal(suspendedAt, rehydrated.SuspendedAt);
        Assert.Null(rehydrated.ArchivedAt);

        await transactionRunner.ExecuteAsync(async ct =>
        {
            var domain = Assert.IsType<AccountingBook>(
                await writeRepository.GetByIdForUpdateAsync(book.Id, ct));
            domain.Archive(archivedAt);
            await writeRepository.UpdateStatusAsync(domain, ct);
        });

        var archived = await readRepository.GetByIdAsync(book.Id);
        Assert.NotNull(archived);
        Assert.Equal("ARCHIVED", archived.Status);
        Assert.Equal(archivedAt, archived.UpdatedAt);
        Assert.Equal(activatedAt, archived.ActivatedAt);
        Assert.Equal(suspendedAt, archived.SuspendedAt);
        Assert.Equal(archivedAt, archived.ArchivedAt);
    }

    [Fact]
    public async Task Seeded_Table_Row_Should_Map_To_Complete_Row_And_Domain_Entity()
    {
        await ResetAccountingDatabaseAsync();
        var createdAt = Utc(2026, 4, 1, 7, 8, 9, 100);
        var updatedAt = Utc(2026, 4, 2, 8, 9, 10, 200);
        var activatedAt = Utc(2026, 4, 3, 9, 10, 11, 300);
        var suspendedAt = Utc(2026, 4, 4, 10, 11, 12, 400);

        await using (var connection = CreateAccountingConnection())
        {
            await connection.OpenAsync();
            await InsertRawAsync(
                connection,
                1_000_000_000_003,
                "BOOK-SEEDED",
                "Seeded book",
                "PORTFOLIO",
                "OWNER-SEEDED",
                "SUSPENDED",
                createdAt,
                updatedAt,
                activatedAt,
                suspendedAt,
                null);
        }

        await using var scope = await CreateScopeAsync();
        var readRepository = scope.Services.GetRequiredService<IAccountingBookReadRepository>();
        var writeRepository = scope.Services.GetRequiredService<IAccountingBookWriteRepository>();
        var transactionRunner = scope.Services.GetRequiredService<IGeneralTransactionRunner>();

        var row = await readRepository.GetByIdAsync(1_000_000_000_003);
        Assert.NotNull(row);
        Assert.Equal(1_000_000_000_003, row.Id);
        Assert.Equal("BOOK-SEEDED", row.Code);
        Assert.Equal("Seeded book", row.Title);
        Assert.Equal("PORTFOLIO", row.OwnerType);
        Assert.Equal("OWNER-SEEDED", row.OwnerId);
        Assert.Equal("SUSPENDED", row.Status);
        Assert.Equal(createdAt, row.CreatedAt);
        Assert.Equal(updatedAt, row.UpdatedAt);
        Assert.Equal(activatedAt, row.ActivatedAt);
        Assert.Equal(suspendedAt, row.SuspendedAt);
        Assert.Null(row.ArchivedAt);

        var domain = await transactionRunner.ExecuteAsync(async ct =>
            Assert.IsType<AccountingBook>(
                await writeRepository.GetByIdForUpdateAsync(row.Id, ct)));

        Assert.Equal(row.Id, domain.Id);
        Assert.Equal(row.Code, domain.Code);
        Assert.Equal(row.Title, domain.Title);
        Assert.Equal(row.OwnerType, domain.OwnerType);
        Assert.Equal(row.OwnerId, domain.OwnerId);
        Assert.Equal(AccountingBookStatus.Suspended, domain.Status);
        Assert.Equal(row.CreatedAt, domain.CreatedAt);
        Assert.Equal(row.UpdatedAt, domain.UpdatedAt);
        Assert.Equal(row.ActivatedAt, domain.ActivatedAt);
        Assert.Equal(row.SuspendedAt, domain.SuspendedAt);
        Assert.Equal(row.ArchivedAt, domain.ArchivedAt);
    }

    [Fact]
    public async Task Database_Should_Enforce_Unique_Code_And_Owner()
    {
        await ResetAccountingDatabaseAsync();
        await using var connection = CreateAccountingConnection();
        await connection.OpenAsync();
        var createdAt = Utc(2026, 5, 1, 0, 0, 0, 0);

        await InsertRawAsync(
            connection,
            1_000_000_000_010,
            "BOOK-UNIQUE",
            "Original",
            "PORTFOLIO",
            "OWNER-UNIQUE",
            "DRAFT",
            createdAt);

        await Assert.ThrowsAsync<SqlException>(() => InsertRawAsync(
            connection,
            1_000_000_000_011,
            "BOOK-UNIQUE",
            "Duplicate code",
            "PORTFOLIO",
            "OWNER-OTHER",
            "DRAFT",
            createdAt));

        await Assert.ThrowsAsync<SqlException>(() => InsertRawAsync(
            connection,
            1_000_000_000_012,
            "BOOK-OTHER",
            "Duplicate owner",
            "PORTFOLIO",
            "OWNER-UNIQUE",
            "DRAFT",
            createdAt));
    }

    [Theory]
    [InlineData(null, "Title", "PORTFOLIO", "OWNER-1", "DRAFT")]
    [InlineData("BOOK-1", null, "PORTFOLIO", "OWNER-1", "DRAFT")]
    [InlineData("BOOK-1", "Title", null, "OWNER-1", "DRAFT")]
    [InlineData("BOOK-1", "Title", "PORTFOLIO", null, "DRAFT")]
    [InlineData("   ", "Title", "PORTFOLIO", "OWNER-1", "DRAFT")]
    [InlineData("BOOK-1", "   ", "PORTFOLIO", "OWNER-1", "DRAFT")]
    [InlineData("BOOK-1", "Title", "   ", "OWNER-1", "DRAFT")]
    [InlineData("BOOK-1", "Title", "PORTFOLIO", "   ", "DRAFT")]
    [InlineData("BOOK-1", "Title", "PORTFOLIO", "OWNER-1", "INVALID")]
    public async Task Database_Should_Reject_Invalid_Required_Values(
        string? code,
        string? title,
        string? ownerType,
        string? ownerId,
        string status)
    {
        await ResetAccountingDatabaseAsync();
        await using var connection = CreateAccountingConnection();
        await connection.OpenAsync();

        await Assert.ThrowsAsync<SqlException>(() => InsertRawAsync(
            connection,
            1_000_000_000_020,
            code,
            title,
            ownerType,
            ownerId,
            status,
            Utc(2026, 5, 2, 0, 0, 0, 0)));
    }

    private static Task<int> InsertRawAsync(
        SqlConnection connection,
        long id,
        string? code,
        string? title,
        string? ownerType,
        string? ownerId,
        string status,
        DateTime createdAt,
        DateTime? updatedAt = null,
        DateTime? activatedAt = null,
        DateTime? suspendedAt = null,
        DateTime? archivedAt = null)
    {
        return connection.ExecuteAsync(
            """
            INSERT INTO accounting_book (
                id,
                code,
                title,
                owner_type,
                owner_id,
                status,
                created_at,
                updated_at,
                activated_at,
                suspended_at,
                archived_at
            )
            VALUES (
                @Id,
                @Code,
                @Title,
                @OwnerType,
                @OwnerId,
                @Status,
                @CreatedAt,
                @UpdatedAt,
                @ActivatedAt,
                @SuspendedAt,
                @ArchivedAt
            )
            """,
            new
            {
                Id = id,
                Code = code,
                Title = title,
                OwnerType = ownerType,
                OwnerId = ownerId,
                Status = status,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
                ActivatedAt = activatedAt,
                SuspendedAt = suspendedAt,
                ArchivedAt = archivedAt
            });
    }

    private static DateTime Utc(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second,
        int millisecond) =>
        new(year, month, day, hour, minute, second, millisecond, DateTimeKind.Utc);
}
