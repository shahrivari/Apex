using Apex.Application.Abstractions.Exceptions;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.DetailAccounts.Domain;
using Apex.Modules.Accounting.DetailAccounts.UseCases.ArchiveDetailAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.CreateDetailAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.DeleteDetailAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.GetDetailAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.GetDetailAccountByCode;
using Apex.Modules.Accounting.DetailAccounts.UseCases.ListDetailAccounts;
using Apex.Modules.Accounting.DetailAccounts.UseCases.ReactivateDetailAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.SearchDetailAccountsForPosting;
using Apex.Modules.Accounting.DetailAccounts.UseCases.UpdateDetailAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.ValidateDetailAccountForPosting;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.IntegrationTests.Accounting.DetailAccounts;

[Collection(ApexIntegrationTestCollection.Name)]
public sealed class DetailAccountHandlerTests(ApexIntegrationTestFixture fixture)
    : ApexIntegrationTestBase(fixture)
{
    [Fact]
    public async Task CreateUpdateGetAndList_ShouldImplementDirectoryWorkflow()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var services = scope.Services;

        var created = await services
            .GetRequiredService<CreateDetailAccountHandler>()
            .HandleAsync(new(" person-100 ", " First Person ", "person"), default);
        Assert.Equal(
            ("PERSON-100", "First Person", "PERSON", "ACTIVE"),
            (created.Code, created.Name, created.Type, created.Status)
        );

        var updated = await services
            .GetRequiredService<UpdateDetailAccountHandler>()
            .HandleAsync(created.Id, new("Renamed", "BANK", created.Code), default);
        Assert.Equal(
            (created.Id, created.Code, "Renamed", "BANK"),
            (updated.Id, updated.Code, updated.Name, updated.Type)
        );

        var byId = await services
            .GetRequiredService<GetDetailAccountHandler>()
            .HandleAsync(created.Id, default);
        var byCode = await services
            .GetRequiredService<GetDetailAccountByCodeHandler>()
            .HandleAsync(" person-100 ", default);
        Assert.Equal(
            byId,
            new GetDetailAccountResponse(
                byCode.Id,
                byCode.Code,
                byCode.Name,
                byCode.Type,
                byCode.Status,
                byCode.CreatedAt,
                byCode.UpdatedAt,
                byCode.ArchivedAt
            )
        );

        var list = await services
            .GetRequiredService<ListDetailAccountsHandler>()
            .HandleAsync(new("bank", "active", "name", 1, 10), default);
        Assert.Equal(1, list.TotalCount);
        Assert.Equal(created.Id, Assert.Single(list.Items).Id);
    }

    [Fact]
    public async Task Create_ShouldRejectValidationUnsupportedTypeAndNormalizedDuplicate()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var handler = scope.Services.GetRequiredService<CreateDetailAccountHandler>();

        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new("", "", "OTHER"), default)
        );
        await handler.HandleAsync(new("duplicate", "Original", "PERSON"), default);
        var duplicate = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(new(" DUPLICATE ", "Other", "BANK"), default)
        );
        Assert.Equal(DetailAccountErrors.CodeAlreadyExists, duplicate.ErrorCode);
    }

    [Fact]
    public async Task Update_ShouldRejectCodeChangeAndMissingAccount()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var created = await scope
            .Services.GetRequiredService<CreateDetailAccountHandler>()
            .HandleAsync(new("IMMUTABLE", "Name", "SYMBOL"), default);
        var update = scope.Services.GetRequiredService<UpdateDetailAccountHandler>();

        var immutable = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            update.HandleAsync(created.Id, new("Name", "BANK", "CHANGED"), default)
        );
        Assert.Equal(DetailAccountErrors.CodeImmutable, immutable.ErrorCode);
        var missing = await Assert.ThrowsAsync<NotFoundException>(() =>
            update.HandleAsync(long.MaxValue, new("Name", "BANK"), default)
        );
        Assert.Equal(DetailAccountErrors.NotFound, missing.ErrorCode);
    }

    [Fact]
    public async Task ArchiveReactivateAndSearch_ShouldEnforceLifecycleEligibility()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var services = scope.Services;
        var created = await services
            .GetRequiredService<CreateDetailAccountHandler>()
            .HandleAsync(new("LIFE", "Lifecycle", "PERSON"), default);
        var archive = services.GetRequiredService<ArchiveDetailAccountHandler>();
        var reactivate = services.GetRequiredService<ReactivateDetailAccountHandler>();
        var search = services.GetRequiredService<SearchDetailAccountsForPostingHandler>();

        Assert.Single((await search.HandleAsync(new("PERSON", "life", 10), default)).Items);
        await archive.HandleAsync(created.Id, default);
        Assert.Empty((await search.HandleAsync(new("PERSON", "life", 10), default)).Items);
        var archivedAgain = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            archive.HandleAsync(created.Id, default)
        );
        Assert.Equal(DetailAccountErrors.AlreadyArchived, archivedAgain.ErrorCode);
        await reactivate.HandleAsync(created.Id, default);
        Assert.Single((await search.HandleAsync(new("PERSON", "life", 10), default)).Items);
        var activeAgain = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            reactivate.HandleAsync(created.Id, default)
        );
        Assert.Equal(DetailAccountErrors.AlreadyActive, activeAgain.ErrorCode);
    }

    [Fact]
    public async Task PostingValidation_ShouldCoverNoneRequiredMissingArchivedAndTypeMismatch()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var services = scope.Services;
        var created = await services
            .GetRequiredService<CreateDetailAccountHandler>()
            .HandleAsync(new("POSTING", "Posting", "PERSON"), default);
        var validator = services.GetRequiredService<IDetailAccountPostingValidator>();

        await validator.ValidateAsync(null, "NONE");
        Assert.Equal(
            DetailAccountErrors.NotAllowed,
            (
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    validator.ValidateAsync(created.Code, "NONE")
                )
            ).ErrorCode
        );
        Assert.Equal(
            DetailAccountErrors.Required,
            (
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    validator.ValidateAsync(null, "PERSON")
                )
            ).ErrorCode
        );
        Assert.Equal(
            DetailAccountErrors.NotFound,
            (
                await Assert.ThrowsAsync<NotFoundException>(() =>
                    validator.ValidateAsync("MISSING", "PERSON")
                )
            ).ErrorCode
        );
        Assert.Equal(
            DetailAccountErrors.TypeMismatch,
            (
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    validator.ValidateAsync(created.Code, "BANK")
                )
            ).ErrorCode
        );
        await validator.ValidateAsync(created.Code, "PERSON");
        await services
            .GetRequiredService<ArchiveDetailAccountHandler>()
            .HandleAsync(created.Id, default);
        Assert.Equal(
            DetailAccountErrors.Archived,
            (
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    validator.ValidateAsync(created.Code, "PERSON")
                )
            ).ErrorCode
        );
    }

    [Fact]
    public async Task Delete_ShouldRemoveUnusedAccountAndPreventCodeReuse()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var services = scope.Services;
        var create = services.GetRequiredService<CreateDetailAccountHandler>();
        var created = await create.HandleAsync(new("DELETE-ME", "Delete", "BANK"), default);

        await services
            .GetRequiredService<DeleteDetailAccountHandler>()
            .HandleAsync(created.Id, default);
        Assert.Equal(
            DetailAccountErrors.NotFound,
            (
                await Assert.ThrowsAsync<NotFoundException>(() =>
                    services
                        .GetRequiredService<GetDetailAccountHandler>()
                        .HandleAsync(created.Id, default)
                )
            ).ErrorCode
        );
        Assert.Equal(
            DetailAccountErrors.CodeAlreadyExists,
            (
                await Assert.ThrowsAsync<ConflictException>(() =>
                    create.HandleAsync(new("DELETE-ME", "Reuse", "BANK"), default)
                )
            ).ErrorCode
        );
    }
}
