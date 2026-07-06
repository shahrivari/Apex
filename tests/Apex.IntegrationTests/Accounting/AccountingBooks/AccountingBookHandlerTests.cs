#pragma warning disable CS8602

using System.Net.Http.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.IntegrationTests.Accounting.AccountingBooks;

using Apex.Application.Abstractions.Exceptions;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.AccountingBooks.Domain;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ActivateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ArchiveAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.GetAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ListAccountingBooks;
using Apex.Modules.Accounting.AccountingBooks.UseCases.SuspendAccountingBook;

[Collection(ApexIntegrationTestCollection.Name)]
public sealed class AccountingBookHandlerTests : ApexIntegrationTestBase
{
    public AccountingBookHandlerTests(ApexIntegrationTestFixture fixture)
        : base(fixture)
    {
    }

    #region Create Handler

    [Fact]
    public async Task Create_Should_PersistAndReturnBook()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var handler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var result = await handler.HandleAsync(new CreateAccountingBookRequest
        {
            Code = "test-001",
            Title = "Test Book",
            OwnerType = "portfolio",
            OwnerId = "100"
        });

        Assert.NotEqual(0, result.Id);
        Assert.Equal("TEST-001", result.Code);
        Assert.Equal("DRAFT", result.Status);

        await using var conn = CreateAccountingConnection();
        await conn.OpenAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM accounting_book WHERE id = @Id", new { Id = result.Id });
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Create_Should_NormalizeCodeAndOwnerType()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var handler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var result = await handler.HandleAsync(new CreateAccountingBookRequest
        {
            Code = "  test-002  ",
            Title = "Test Book",
            OwnerType = "  portfolio  ",
            OwnerId = "  101  "
        });

        Assert.Equal("TEST-002", result.Code);

        var getHandler = scope.Services.GetRequiredService<GetAccountingBookHandler>();
        var book = await getHandler.HandleAsync(result.Id);

        Assert.Equal("PORTFOLIO", book.OwnerType);
        Assert.Equal("101", book.OwnerId);
    }

    [Fact]
    public async Task Create_DuplicateCode_Should_ThrowConflict()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var handler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();

        await handler.HandleAsync(new CreateAccountingBookRequest
        {
            Code = "unique-code",
            Title = "First",
            OwnerType = "PORTFOLIO",
            OwnerId = "200"
        });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(new CreateAccountingBookRequest
            {
                Code = "unique-code",
                Title = "Second",
                OwnerType = "FUND",
                OwnerId = "201"
            }));

        Assert.Equal(AccountingBookErrors.AccountingBookCodeAlreadyExists, ex.ErrorCode);
    }

    [Fact]
    public async Task Create_DuplicateOwner_Should_ThrowConflict()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var handler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();

        await handler.HandleAsync(new CreateAccountingBookRequest
        {
            Code = "code-a",
            Title = "First",
            OwnerType = "PORTFOLIO",
            OwnerId = "300"
        });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(new CreateAccountingBookRequest
            {
                Code = "code-b",
                Title = "Second",
                OwnerType = "PORTFOLIO",
                OwnerId = "300"
            }));

        Assert.Equal(AccountingBookErrors.AccountingBookOwnerAlreadyExists, ex.ErrorCode);
    }

    #endregion

    #region Get Handler

    [Fact]
    public async Task Get_Existing_Should_ReturnFullBook()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var book = await createHandler.HandleAsync(new CreateAccountingBookRequest
        {
            Code = "get-test",
            Title = "Get Test Book",
            OwnerType = "FUND",
            OwnerId = "400"
        });

        var getHandler = scope.Services.GetRequiredService<GetAccountingBookHandler>();
        var result = await getHandler.HandleAsync(book.Id);

        Assert.Equal(book.Id, result.Id);
        Assert.Equal("GET-TEST", result.Code);
        Assert.Equal("Get Test Book", result.Title);
        Assert.Equal("FUND", result.OwnerType);
        Assert.Equal("400", result.OwnerId);
        Assert.Equal("DRAFT", result.Status);
    }

    [Fact]
    public async Task Get_NotFound_Should_ThrowNotFound()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var handler = scope.Services.GetRequiredService<GetAccountingBookHandler>();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(99999999));

        Assert.Equal(AccountingBookErrors.AccountingBookNotFound, ex.ErrorCode);
    }

    #endregion

    #region List Handler

    [Fact]
    public async Task List_EmptyDb_Should_ReturnEmpty()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var handler = scope.Services.GetRequiredService<ListAccountingBooksHandler>();
        var result = await handler.HandleAsync(new ListAccountingBooksRequest());

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task List_Multiple_Should_ReturnAll()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "list-a", Title = "A", OwnerType = "PORTFOLIO", OwnerId = "500" });
        await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "list-b", Title = "B", OwnerType = "PORTFOLIO", OwnerId = "501" });
        await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "list-c", Title = "C", OwnerType = "PORTFOLIO", OwnerId = "502" });

        var listHandler = scope.Services.GetRequiredService<ListAccountingBooksHandler>();
        var result = await listHandler.HandleAsync(new ListAccountingBooksRequest());

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task List_FilterByStatus_Should_ReturnMatching()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var activeBook = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "list-active", Title = "Active", OwnerType = "PORTFOLIO", OwnerId = "600" });
        await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "list-draft", Title = "Draft", OwnerType = "PORTFOLIO", OwnerId = "601" });

        var activateHandler = scope.Services.GetRequiredService<ActivateAccountingBookHandler>();
        await activateHandler.HandleAsync(activeBook.Id);

        var listHandler = scope.Services.GetRequiredService<ListAccountingBooksHandler>();
        var result = await listHandler.HandleAsync(new ListAccountingBooksRequest { Status = "ACTIVE" });

        Assert.Single(result.Items);
        Assert.Equal("LIST-ACTIVE", result.Items[0].Code);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task List_Pagination_Should_Work()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        for (var i = 0; i < 7; i++)
        {
            await createHandler.HandleAsync(new CreateAccountingBookRequest
            {
                Code = $"page-{i:D2}",
                Title = $"Page {i}",
                OwnerType = "PORTFOLIO",
                OwnerId = $"page-{i}"
            });
        }

        var listHandler = scope.Services.GetRequiredService<ListAccountingBooksHandler>();

        var page1 = await listHandler.HandleAsync(new ListAccountingBooksRequest { Page = 1, PageSize = 3 });
        var page2 = await listHandler.HandleAsync(new ListAccountingBooksRequest { Page = 2, PageSize = 3 });
        var page3 = await listHandler.HandleAsync(new ListAccountingBooksRequest { Page = 3, PageSize = 3 });

        Assert.Equal(3, page1.Items.Count);
        Assert.Equal(3, page2.Items.Count);
        Assert.Single(page3.Items);

        Assert.Equal(7, page1.TotalCount);
        Assert.Equal(7, page2.TotalCount);
        Assert.Equal(7, page3.TotalCount);
    }

    #endregion

    #region Activate Handler

    [Fact]
    public async Task Activate_DraftToActive_Should_Succeed()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var book = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "act-1", Title = "T", OwnerType = "PORTFOLIO", OwnerId = "700" });

        var handler = scope.Services.GetRequiredService<ActivateAccountingBookHandler>();
        var result = await handler.HandleAsync(book.Id);

        Assert.Equal("ACTIVE", result.Status);
        Assert.NotNull(result.ActivatedAt);

        await using var conn = CreateAccountingConnection();
        await conn.OpenAsync();
        var dbStatus = await conn.ExecuteScalarAsync<string>(
            "SELECT status FROM accounting_book WHERE id = @Id", new { Id = book.Id });
        Assert.Equal("ACTIVE", dbStatus);
    }

    [Fact]
    public async Task Activate_SuspendedToActive_Should_Succeed()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var book = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "act-2", Title = "T", OwnerType = "PORTFOLIO", OwnerId = "701" });

        var activateHandler = scope.Services.GetRequiredService<ActivateAccountingBookHandler>();
        var suspendHandler = scope.Services.GetRequiredService<SuspendAccountingBookHandler>();

        await activateHandler.HandleAsync(book.Id);
        await suspendHandler.HandleAsync(book.Id);

        var result = await activateHandler.HandleAsync(book.Id);
        Assert.Equal("ACTIVE", result.Status);
    }

    [Fact]
    public async Task Activate_AlreadyActive_Should_Throw()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var book = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "act-3", Title = "T", OwnerType = "PORTFOLIO", OwnerId = "702" });

        var activateHandler = scope.Services.GetRequiredService<ActivateAccountingBookHandler>();
        await activateHandler.HandleAsync(book.Id);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            activateHandler.HandleAsync(book.Id));

        Assert.Equal(AccountingBookErrors.AccountingBookCannotBeActivated, ex.ErrorCode);
    }

    [Fact]
    public async Task Activate_Archived_Should_Throw()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var book = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "act-4", Title = "T", OwnerType = "PORTFOLIO", OwnerId = "703" });

        var archiveHandler = scope.Services.GetRequiredService<ArchiveAccountingBookHandler>();
        await archiveHandler.HandleAsync(book.Id);

        var activateHandler = scope.Services.GetRequiredService<ActivateAccountingBookHandler>();
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            activateHandler.HandleAsync(book.Id));

        Assert.Equal(AccountingBookErrors.AccountingBookCannotBeActivated, ex.ErrorCode);
    }

    [Fact]
    public async Task Activate_NotFound_Should_ThrowNotFound()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var handler = scope.Services.GetRequiredService<ActivateAccountingBookHandler>();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(99999999));

        Assert.Equal(AccountingBookErrors.AccountingBookNotFound, ex.ErrorCode);
    }

    #endregion

    #region Suspend Handler

    [Fact]
    public async Task Suspend_ActiveToSuspended_Should_Succeed()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var book = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "sus-1", Title = "T", OwnerType = "PORTFOLIO", OwnerId = "800" });

        var activateHandler = scope.Services.GetRequiredService<ActivateAccountingBookHandler>();
        await activateHandler.HandleAsync(book.Id);

        var handler = scope.Services.GetRequiredService<SuspendAccountingBookHandler>();
        var result = await handler.HandleAsync(book.Id);

        Assert.Equal("SUSPENDED", result.Status);
        Assert.NotNull(result.SuspendedAt);
    }

    [Fact]
    public async Task Suspend_Draft_Should_Throw()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var book = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "sus-2", Title = "T", OwnerType = "PORTFOLIO", OwnerId = "801" });

        var handler = scope.Services.GetRequiredService<SuspendAccountingBookHandler>();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.HandleAsync(book.Id));

        Assert.Equal(AccountingBookErrors.AccountingBookCannotBeSuspended, ex.ErrorCode);
    }

    [Fact]
    public async Task Suspend_Archived_Should_Throw()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var book = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "sus-3", Title = "T", OwnerType = "PORTFOLIO", OwnerId = "802" });

        var archiveHandler = scope.Services.GetRequiredService<ArchiveAccountingBookHandler>();
        await archiveHandler.HandleAsync(book.Id);

        var suspendHandler = scope.Services.GetRequiredService<SuspendAccountingBookHandler>();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            suspendHandler.HandleAsync(book.Id));

        Assert.Equal(AccountingBookErrors.AccountingBookCannotBeSuspended, ex.ErrorCode);
    }

    #endregion

    #region Archive Handler

    [Fact]
    public async Task Archive_DraftToArchived_Should_Succeed()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var book = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "arc-1", Title = "T", OwnerType = "PORTFOLIO", OwnerId = "900" });

        var handler = scope.Services.GetRequiredService<ArchiveAccountingBookHandler>();
        var result = await handler.HandleAsync(book.Id);

        Assert.Equal("ARCHIVED", result.Status);
        Assert.NotNull(result.ArchivedAt);
    }

    [Fact]
    public async Task Archive_SuspendedToArchived_Should_Succeed()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var book = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "arc-2", Title = "T", OwnerType = "PORTFOLIO", OwnerId = "901" });

        var activateHandler = scope.Services.GetRequiredService<ActivateAccountingBookHandler>();
        var suspendHandler = scope.Services.GetRequiredService<SuspendAccountingBookHandler>();
        var archiveHandler = scope.Services.GetRequiredService<ArchiveAccountingBookHandler>();

        await activateHandler.HandleAsync(book.Id);
        await suspendHandler.HandleAsync(book.Id);

        var result = await archiveHandler.HandleAsync(book.Id);
        Assert.Equal("ARCHIVED", result.Status);
    }

    [Fact]
    public async Task Archive_Active_Should_Throw()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var book = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "arc-3", Title = "T", OwnerType = "PORTFOLIO", OwnerId = "902" });

        var activateHandler = scope.Services.GetRequiredService<ActivateAccountingBookHandler>();
        await activateHandler.HandleAsync(book.Id);

        var archiveHandler = scope.Services.GetRequiredService<ArchiveAccountingBookHandler>();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            archiveHandler.HandleAsync(book.Id));

        Assert.Equal(AccountingBookErrors.AccountingBookCannotBeArchived, ex.ErrorCode);
    }

    [Fact]
    public async Task Archive_AlreadyArchived_Should_Throw()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var book = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "arc-4", Title = "T", OwnerType = "PORTFOLIO", OwnerId = "903" });

        var archiveHandler = scope.Services.GetRequiredService<ArchiveAccountingBookHandler>();
        await archiveHandler.HandleAsync(book.Id);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            archiveHandler.HandleAsync(book.Id));

        Assert.Equal(AccountingBookErrors.AccountingBookCannotBeArchived, ex.ErrorCode);
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public async Task FullLifecycle_Should_Work()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var activateHandler = scope.Services.GetRequiredService<ActivateAccountingBookHandler>();
        var suspendHandler = scope.Services.GetRequiredService<SuspendAccountingBookHandler>();
        var archiveHandler = scope.Services.GetRequiredService<ArchiveAccountingBookHandler>();
        var getHandler = scope.Services.GetRequiredService<GetAccountingBookHandler>();

        var book = await createHandler.HandleAsync(new CreateAccountingBookRequest
        {
            Code = "lifecycle",
            Title = "Lifecycle Test",
            OwnerType = "PORTFOLIO",
            OwnerId = "1000"
        });
        Assert.Equal("DRAFT", book.Status);

        var activated = await activateHandler.HandleAsync(book.Id);
        Assert.Equal("ACTIVE", activated.Status);
        Assert.NotNull(activated.ActivatedAt);

        var suspended = await suspendHandler.HandleAsync(book.Id);
        Assert.Equal("SUSPENDED", suspended.Status);

        var reactivated = await activateHandler.HandleAsync(book.Id);
        Assert.Equal("ACTIVE", reactivated.Status);

        var reSuspended = await suspendHandler.HandleAsync(book.Id);
        Assert.Equal("SUSPENDED", reSuspended.Status);

        var archived = await archiveHandler.HandleAsync(book.Id);
        Assert.Equal("ARCHIVED", archived.Status);

        var final = await getHandler.HandleAsync(book.Id);
        Assert.Equal("ARCHIVED", final.Status);
        Assert.NotNull(final.ActivatedAt);
        Assert.NotNull(final.SuspendedAt);
        Assert.NotNull(final.ArchivedAt);
    }

    [Fact]
    public async Task MultipleBooks_IndependentState()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var activateHandler = scope.Services.GetRequiredService<ActivateAccountingBookHandler>();
        var archiveHandler = scope.Services.GetRequiredService<ArchiveAccountingBookHandler>();
        var getHandler = scope.Services.GetRequiredService<GetAccountingBookHandler>();

        var bookA = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "multi-a", Title = "A", OwnerType = "PORTFOLIO", OwnerId = "1100" });
        var bookB = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "multi-b", Title = "B", OwnerType = "FUND", OwnerId = "1101" });

        await activateHandler.HandleAsync(bookA.Id);
        await archiveHandler.HandleAsync(bookB.Id);

        var a = await getHandler.HandleAsync(bookA.Id);
        var b = await getHandler.HandleAsync(bookB.Id);

        Assert.Equal("ACTIVE", a.Status);
        Assert.Equal("ARCHIVED", b.Status);

        await using var conn = CreateAccountingConnection();
        await conn.OpenAsync();
        var aStatus = await conn.ExecuteScalarAsync<string>(
            "SELECT status FROM accounting_book WHERE id = @Id", new { Id = bookA.Id });
        var bStatus = await conn.ExecuteScalarAsync<string>(
            "SELECT status FROM accounting_book WHERE id = @Id", new { Id = bookB.Id });
        Assert.Equal("ACTIVE", aStatus);
        Assert.Equal("ARCHIVED", bStatus);
    }

    [Fact]
    public async Task ConflictRecovery_Should_NotLoseData()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();

        var createHandler = scope.Services.GetRequiredService<CreateAccountingBookHandler>();
        var listHandler = scope.Services.GetRequiredService<ListAccountingBooksHandler>();

        var bookA = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "conflict-a", Title = "A", OwnerType = "PORTFOLIO", OwnerId = "1200" });

        await Assert.ThrowsAsync<ConflictException>(() =>
            createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "conflict-a", Title = "Fail", OwnerType = "FUND", OwnerId = "1201" }));

        var bookB = await createHandler.HandleAsync(new CreateAccountingBookRequest { Code = "conflict-b", Title = "B", OwnerType = "FUND", OwnerId = "1201" });

        var result = await listHandler.HandleAsync(new ListAccountingBooksRequest());
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalCount);

        Assert.Contains(result.Items, x => x.Id == bookA.Id);
        Assert.Contains(result.Items, x => x.Id == bookB.Id);
    }

    #endregion
}
