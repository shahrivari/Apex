using Apex.Application.Abstractions.Exceptions;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ActivateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ArchiveAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using Apex.Modules.Accounting.FiscalYears.UseCases.CancelFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.CreateFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.GetFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.OpenFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.ResolveFiscalYear;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.IntegrationTests.Accounting.FiscalYears;

[Collection(ApexIntegrationTestCollection.Name)]
public sealed class FiscalYearHandlerTests(ApexIntegrationTestFixture fixture)
    : ApexIntegrationTestBase(fixture)
{
    [Fact]
    public async Task CreateReadAndResolve_ShouldCommitCompleteFiscalYear()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var bookId = await CreateBookAsync(scope, "FY-ROUNDTRIP", "fy-roundtrip");

        var created = await scope.Services.GetRequiredService<CreateFiscalYearHandler>().HandleAsync(
            Request(bookId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "Fiscal 2026"));

        var row = await scope.Services.GetRequiredService<IFiscalYearReadRepository>().GetByIdAsync(created.Id);
        Assert.NotNull(row);
        Assert.Equal(bookId, row.AccountingBookId);
        Assert.Equal("Fiscal 2026", row.Title);
        Assert.Equal(new DateOnly(2025, 12, 31), row.FinalizedThroughDate);
        Assert.Equal("DRAFT", row.Status);
        Assert.Equal(1, row.NextReferenceNumber);
        Assert.Equal(1, row.NextJournalEntryNumber);
        Assert.Null(row.UpdatedAt);
        Assert.Null(row.OpenedAt);
        Assert.Null(row.ClosedAt);
        Assert.Null(row.CancelledAt);
        Assert.Null(row.CancellationDate);

        var resolved = await scope.Services.GetRequiredService<ResolveFiscalYearHandler>().HandleAsync(
            new ResolveFiscalYearRequest { AccountingBookId = bookId, AccountingDate = new DateOnly(2026, 6, 1) });
        Assert.Equal(created.Id, resolved.Id);
    }

    [Fact]
    public async Task Create_OverlappingRange_ShouldRollbackWithStableConflict()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var bookId = await CreateBookAsync(scope, "FY-OVERLAP", "fy-overlap");
        var handler = scope.Services.GetRequiredService<CreateFiscalYearHandler>();
        await handler.HandleAsync(Request(bookId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));

        var exception = await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            Request(bookId, new DateOnly(2026, 12, 1), new DateOnly(2027, 11, 30))));

        Assert.Equal(FiscalYearErrors.DatesOverlap, exception.ErrorCode);
        var years = await scope.Services.GetRequiredService<IFiscalYearDirectoryRepository>()
            .ListAsync(bookId, null, null, null, 1, 10);
        Assert.Single(years.Items);
    }

    [Fact]
    public async Task Create_ForArchivedBook_ShouldBeRejected()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var bookId = await CreateBookAsync(scope, "FY-ARCHIVED", "fy-archived", activate: false);
        await scope.Services.GetRequiredService<ArchiveAccountingBookHandler>().HandleAsync(bookId);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            scope.Services.GetRequiredService<CreateFiscalYearHandler>().HandleAsync(
                Request(bookId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31))));

        Assert.Equal(FiscalYearErrors.AccountingBookArchived, exception.ErrorCode);
    }

    [Fact]
    public async Task Open_ForNonActiveBook_ShouldBeRejected()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var bookId = await CreateBookAsync(scope, "FY-DRAFT-BOOK", "fy-draft-book", activate: false);
        var fiscalYear = await scope.Services.GetRequiredService<CreateFiscalYearHandler>().HandleAsync(
            Request(bookId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            scope.Services.GetRequiredService<OpenFiscalYearHandler>().HandleAsync(fiscalYear.Id));

        Assert.Equal(FiscalYearErrors.AccountingBookNotActive, exception.ErrorCode);
    }

    [Fact]
    public async Task OpenFinalizeAndCancel_ShouldCommitLifecycle()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var bookId = await CreateBookAsync(scope, "FY-LIFECYCLE", "fy-lifecycle");
        var created = await scope.Services.GetRequiredService<CreateFiscalYearHandler>().HandleAsync(
            Request(bookId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));

        await scope.Services.GetRequiredService<OpenFiscalYearHandler>().HandleAsync(created.Id);
        var cancellationDate = new DateOnly(2026, 6, 30);
        await scope.Services.GetRequiredService<FinalizeFiscalYearHandler>().HandleAsync(created.Id,
            new FinalizeFiscalYearRequest { FinalizedThroughDate = cancellationDate });
        await scope.Services.GetRequiredService<CancelFiscalYearHandler>().HandleAsync(created.Id,
            new CancelFiscalYearRequest { CancellationDate = cancellationDate });

        var result = await scope.Services.GetRequiredService<GetFiscalYearHandler>().HandleAsync(created.Id);
        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal(cancellationDate, result.FinalizedThroughDate);
        Assert.Equal(cancellationDate, result.CancellationDate);
        Assert.Equal(1, result.NextReferenceNumber);
        Assert.Equal(1, result.NextJournalEntryNumber);
        Assert.NotNull(result.OpenedAt);
        Assert.NotNull(result.CancelledAt);
    }

    [Fact]
    public async Task Create_AfterCancelledEffectiveEnd_ShouldNotOverlapOriginalRange()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var bookId = await CreateBookAsync(scope, "FY-CANCELLED-RANGE", "fy-cancelled-range");
        var createHandler = scope.Services.GetRequiredService<CreateFiscalYearHandler>();
        var first = await createHandler.HandleAsync(
            Request(bookId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        var cancellationDate = new DateOnly(2026, 6, 30);

        await scope.Services.GetRequiredService<OpenFiscalYearHandler>().HandleAsync(first.Id);
        await scope.Services.GetRequiredService<FinalizeFiscalYearHandler>().HandleAsync(first.Id,
            new FinalizeFiscalYearRequest { FinalizedThroughDate = cancellationDate });
        await scope.Services.GetRequiredService<CancelFiscalYearHandler>().HandleAsync(first.Id,
            new CancelFiscalYearRequest { CancellationDate = cancellationDate });
        var replacement = await createHandler.HandleAsync(
            Request(bookId, cancellationDate.AddDays(1), new DateOnly(2026, 12, 31), "Replacement"));

        Assert.Equal(cancellationDate.AddDays(1), replacement.StartDate);
    }

    private static CreateFiscalYearRequest Request(
        long bookId, DateOnly startDate, DateOnly endDate, string? title = null) => new()
        {
            AccountingBookId = bookId,
            Title = title ?? startDate.Year.ToString(),
            StartDate = startDate,
            EndDate = endDate
        };

    private static async Task<long> CreateBookAsync(
        ServiceScopeHandle scope, string code, string ownerId, bool activate = true)
    {
        var result = await scope.Services.GetRequiredService<CreateAccountingBookHandler>().HandleAsync(
            new CreateAccountingBookRequest
            {
                Code = code,
                Title = code,
                OwnerType = "TEST",
                OwnerId = ownerId
            });
        if (activate)
            await scope.Services.GetRequiredService<ActivateAccountingBookHandler>().HandleAsync(result.Id);
        return result.Id;
    }
}
