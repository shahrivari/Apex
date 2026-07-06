using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.AccountingBooks.Domain;

namespace Apex.UnitTests.Accounting.AccountingBooks.Domain;

public class AccountingBookDomainTests
{
    [Fact]
    public void Create_Should_SetDraftStatus()
    {
        var book = AccountingBook.Create(1, "ABC", "Test", "PORTFOLIO", "123", DateTime.UtcNow);

        Assert.Equal(AccountingBookStatus.Draft, book.Status);
    }

    [Fact]
    public void Create_Should_NormalizeCodeAndOwnerType()
    {
        var book = AccountingBook.Create(1, "  abc  ", "Test", "  portfolio  ", "  123  ", DateTime.UtcNow);

        Assert.Equal("ABC", book.Code);
        Assert.Equal("PORTFOLIO", book.OwnerType);
        Assert.Equal("123", book.OwnerId);
        Assert.Equal("Test", book.Title);
    }

    [Fact]
    public void Create_Should_ThrowOnEmptyCode()
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            AccountingBook.Create(1, "", "Test", "PORTFOLIO", "123", DateTime.UtcNow));

        Assert.Equal("invalid_accounting_book_code", ex.ErrorCode);
    }

    [Fact]
    public void Create_Should_ThrowOnEmptyTitle()
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            AccountingBook.Create(1, "CODE", "", "PORTFOLIO", "123", DateTime.UtcNow));

        Assert.Equal("invalid_accounting_book_title", ex.ErrorCode);
    }

    [Fact]
    public void Create_Should_ThrowOnEmptyOwnerType()
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            AccountingBook.Create(1, "CODE", "Test", "", "123", DateTime.UtcNow));

        Assert.Equal("invalid_accounting_book_owner", ex.ErrorCode);
    }

    [Fact]
    public void Create_Should_ThrowOnEmptyOwnerId()
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            AccountingBook.Create(1, "CODE", "Test", "PORTFOLIO", "", DateTime.UtcNow));

        Assert.Equal("invalid_accounting_book_owner", ex.ErrorCode);
    }

    [Fact]
    public void Activate_Should_TransitionDraftToActive()
    {
        var book = AccountingBook.Create(1, "ABC", "Test", "PORTFOLIO", "123", DateTime.UtcNow);
        var now = DateTime.UtcNow;

        book.Activate(now);

        Assert.Equal(AccountingBookStatus.Active, book.Status);
        Assert.Equal(now, book.ActivatedAt);
        Assert.Equal(now, book.UpdatedAt);
    }

    [Fact]
    public void Activate_Should_TransitionSuspendedToActive()
    {
        var book = AccountingBook.Create(1, "ABC", "Test", "PORTFOLIO", "123", DateTime.UtcNow);
        book.Activate(DateTime.UtcNow);
        book.Suspend(DateTime.UtcNow);

        book.Activate(DateTime.UtcNow);

        Assert.Equal(AccountingBookStatus.Active, book.Status);
    }

    [Fact]
    public void Activate_Should_ThrowForActive()
    {
        var book = AccountingBook.Create(1, "ABC", "Test", "PORTFOLIO", "123", DateTime.UtcNow);
        book.Activate(DateTime.UtcNow);

        var ex = Assert.Throws<BusinessRuleException>(() => book.Activate(DateTime.UtcNow));

        Assert.Equal(AccountingBookErrors.AccountingBookCannotBeActivated, ex.ErrorCode);
    }

    [Fact]
    public void Activate_Should_ThrowForArchived()
    {
        var book = AccountingBook.Create(1, "ABC", "Test", "PORTFOLIO", "123", DateTime.UtcNow);
        book.Archive(DateTime.UtcNow);

        var ex = Assert.Throws<BusinessRuleException>(() => book.Activate(DateTime.UtcNow));

        Assert.Equal(AccountingBookErrors.AccountingBookCannotBeActivated, ex.ErrorCode);
    }

    [Fact]
    public void Suspend_Should_TransitionActiveToSuspended()
    {
        var book = AccountingBook.Create(1, "ABC", "Test", "PORTFOLIO", "123", DateTime.UtcNow);
        book.Activate(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        book.Suspend(now);

        Assert.Equal(AccountingBookStatus.Suspended, book.Status);
        Assert.Equal(now, book.SuspendedAt);
        Assert.Equal(now, book.UpdatedAt);
    }

    [Fact]
    public void Suspend_Should_ThrowForDraft()
    {
        var book = AccountingBook.Create(1, "ABC", "Test", "PORTFOLIO", "123", DateTime.UtcNow);

        var ex = Assert.Throws<BusinessRuleException>(() => book.Suspend(DateTime.UtcNow));

        Assert.Equal(AccountingBookErrors.AccountingBookCannotBeSuspended, ex.ErrorCode);
    }

    [Fact]
    public void Suspend_Should_ThrowForArchived()
    {
        var book = AccountingBook.Create(1, "ABC", "Test", "PORTFOLIO", "123", DateTime.UtcNow);
        book.Archive(DateTime.UtcNow);

        var ex = Assert.Throws<BusinessRuleException>(() => book.Suspend(DateTime.UtcNow));

        Assert.Equal(AccountingBookErrors.AccountingBookCannotBeSuspended, ex.ErrorCode);
    }

    [Fact]
    public void Archive_Should_TransitionDraftToArchived()
    {
        var book = AccountingBook.Create(1, "ABC", "Test", "PORTFOLIO", "123", DateTime.UtcNow);
        var now = DateTime.UtcNow;

        book.Archive(now);

        Assert.Equal(AccountingBookStatus.Archived, book.Status);
        Assert.Equal(now, book.ArchivedAt);
    }

    [Fact]
    public void Archive_Should_TransitionSuspendedToArchived()
    {
        var book = AccountingBook.Create(1, "ABC", "Test", "PORTFOLIO", "123", DateTime.UtcNow);
        book.Activate(DateTime.UtcNow);
        book.Suspend(DateTime.UtcNow);

        book.Archive(DateTime.UtcNow);

        Assert.Equal(AccountingBookStatus.Archived, book.Status);
    }

    [Fact]
    public void Archive_Should_ThrowForActive()
    {
        var book = AccountingBook.Create(1, "ABC", "Test", "PORTFOLIO", "123", DateTime.UtcNow);
        book.Activate(DateTime.UtcNow);

        var ex = Assert.Throws<BusinessRuleException>(() => book.Archive(DateTime.UtcNow));

        Assert.Equal(AccountingBookErrors.AccountingBookCannotBeArchived, ex.ErrorCode);
    }

    [Fact]
    public void Archive_Should_ThrowForAlreadyArchived()
    {
        var book = AccountingBook.Create(1, "ABC", "Test", "PORTFOLIO", "123", DateTime.UtcNow);
        book.Archive(DateTime.UtcNow);

        var ex = Assert.Throws<BusinessRuleException>(() => book.Archive(DateTime.UtcNow));

        Assert.Equal(AccountingBookErrors.AccountingBookCannotBeArchived, ex.ErrorCode);
    }

    [Fact]
    public void StatusToDatabaseValue_Should_ReturnCorrectString()
    {
        Assert.Equal("DRAFT", AccountingBookStatus.Draft.ToDatabaseValue());
        Assert.Equal("ACTIVE", AccountingBookStatus.Active.ToDatabaseValue());
        Assert.Equal("SUSPENDED", AccountingBookStatus.Suspended.ToDatabaseValue());
        Assert.Equal("ARCHIVED", AccountingBookStatus.Archived.ToDatabaseValue());
    }

    [Fact]
    public void StatusFromDatabaseValue_Should_ReturnCorrectEnum()
    {
        Assert.Equal(AccountingBookStatus.Draft, AccountingBookStatusExtensions.FromDatabaseValue("DRAFT"));
        Assert.Equal(AccountingBookStatus.Active, AccountingBookStatusExtensions.FromDatabaseValue("ACTIVE"));
        Assert.Equal(AccountingBookStatus.Suspended, AccountingBookStatusExtensions.FromDatabaseValue("SUSPENDED"));
        Assert.Equal(AccountingBookStatus.Archived, AccountingBookStatusExtensions.FromDatabaseValue("ARCHIVED"));
    }
}
