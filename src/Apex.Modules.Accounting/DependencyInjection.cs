namespace Apex.Modules.Accounting;

using Apex.Modules.Accounting.AccountingBooks.UseCases.ActivateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ArchiveAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.GetAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ListAccountingBooks;
using Apex.Modules.Accounting.AccountingBooks.Repositories;
using Apex.Modules.Accounting.AccountingBooks.UseCases.SuspendAccountingBook;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using Apex.Modules.Accounting.FiscalYears.UseCases.AllocateDocumentNumber;
using Apex.Modules.Accounting.FiscalYears.UseCases.CancelFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.CreateFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.DeleteFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.GetFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.ListFiscalYears;
using Apex.Modules.Accounting.FiscalYears.UseCases.OpenFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.ResolveFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.UpdateFiscalYear;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories;
using Apex.Modules.Accounting.DetailAccounts.Repositories;
using Apex.Modules.Accounting.DetailAccounts.UseCases.ValidateDetailAccountForPosting;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Repositories
        services.AddScoped<IAccountingBookReadRepository, AccountingBookReadRepository>();
        services.AddScoped<IAccountingBookWriteRepository, AccountingBookWriteRepository>();
        services.AddScoped<IFiscalYearReadRepository, FiscalYearReadRepository>();
        services.AddScoped<IFiscalYearWriteRepository, FiscalYearWriteRepository>();
        services.AddScoped<IAccountClassReadRepository, AccountClassReadRepository>();
        services.AddScoped<IAccountClassWriteRepository, AccountClassWriteRepository>();
        services.AddScoped<IGeneralAccountReadRepository, GeneralAccountReadRepository>();
        services.AddScoped<IGeneralAccountWriteRepository, GeneralAccountWriteRepository>();
        services.AddScoped<ISubsidiaryAccountReadRepository, SubsidiaryAccountReadRepository>();
        services.AddScoped<ISubsidiaryAccountWriteRepository, SubsidiaryAccountWriteRepository>();
        services.AddScoped<IDetailAccountReadRepository, DetailAccountReadRepository>();
        services.AddScoped<IDetailAccountWriteRepository, DetailAccountWriteRepository>();
        services.AddScoped<IDetailAccountPostingValidator, ValidateDetailAccountForPostingHandler>();

        // Handlers
        services.AddTransient<CreateAccountingBookHandler>();
        services.AddTransient<GetAccountingBookHandler>();
        services.AddTransient<ListAccountingBooksHandler>();
        services.AddTransient<ActivateAccountingBookHandler>();
        services.AddTransient<SuspendAccountingBookHandler>();
        services.AddTransient<ArchiveAccountingBookHandler>();
        services.AddTransient<CreateFiscalYearHandler>();
        services.AddTransient<UpdateFiscalYearHandler>();
        services.AddTransient<DeleteFiscalYearHandler>();
        services.AddTransient<GetFiscalYearHandler>();
        services.AddTransient<ListFiscalYearsHandler>();
        services.AddTransient<OpenFiscalYearHandler>();
        services.AddTransient<ResolveFiscalYearHandler>();
        services.AddTransient<FinalizeFiscalYearHandler>();
        services.AddTransient<CancelFiscalYearHandler>();
        services.AddTransient<AllocateDocumentNumberHandler>();
        services.AddTransient<ChartOfAccounts.UseCases.CreateAccountClass.CreateAccountClassHandler>();
        services.AddTransient<ChartOfAccounts.UseCases.UpdateAccountClass.UpdateAccountClassHandler>();
        services.AddTransient<ChartOfAccounts.UseCases.ArchiveAccountClass.ArchiveAccountClassHandler>();
        services.AddTransient<ChartOfAccounts.UseCases.ReactivateAccountClass.ReactivateAccountClassHandler>();
        services.AddTransient<ChartOfAccounts.UseCases.CreateGeneralAccount.CreateGeneralAccountHandler>();
        services.AddTransient<ChartOfAccounts.UseCases.UpdateGeneralAccount.UpdateGeneralAccountHandler>();
        services.AddTransient<ChartOfAccounts.UseCases.ArchiveGeneralAccount.ArchiveGeneralAccountHandler>();
        services.AddTransient<ChartOfAccounts.UseCases.ReactivateGeneralAccount.ReactivateGeneralAccountHandler>();
        services.AddTransient<ChartOfAccounts.UseCases.CreateSubsidiaryAccount.CreateSubsidiaryAccountHandler>();
        services.AddTransient<ChartOfAccounts.UseCases.UpdateSubsidiaryAccount.UpdateSubsidiaryAccountHandler>();
        services.AddTransient<ChartOfAccounts.UseCases.ArchiveSubsidiaryAccount.ArchiveSubsidiaryAccountHandler>();
        services.AddTransient<ChartOfAccounts.UseCases.ReactivateSubsidiaryAccount.ReactivateSubsidiaryAccountHandler>();
        services.AddTransient<ChartOfAccounts.UseCases.GetAccount.GetAccountHandler>();
        services.AddTransient<ChartOfAccounts.UseCases.GetAccountTree.GetAccountTreeHandler>();
        services.AddTransient<ChartOfAccounts.UseCases.SearchAccounts.SearchAccountsHandler>();
        services.AddTransient<DetailAccounts.UseCases.CreateDetailAccount.CreateDetailAccountHandler>();
        services.AddTransient<DetailAccounts.UseCases.UpdateDetailAccount.UpdateDetailAccountHandler>();
        services.AddTransient<DetailAccounts.UseCases.GetDetailAccount.GetDetailAccountHandler>();
        services.AddTransient<DetailAccounts.UseCases.GetDetailAccountByCode.GetDetailAccountByCodeHandler>();
        services.AddTransient<DetailAccounts.UseCases.ListDetailAccounts.ListDetailAccountsHandler>();
        services.AddTransient<DetailAccounts.UseCases.SearchDetailAccountsForPosting.SearchDetailAccountsForPostingHandler>();
        services.AddTransient<DetailAccounts.UseCases.ArchiveDetailAccount.ArchiveDetailAccountHandler>();
        services.AddTransient<DetailAccounts.UseCases.ReactivateDetailAccount.ReactivateDetailAccountHandler>();
        services.AddTransient<DetailAccounts.UseCases.DeleteDetailAccount.DeleteDetailAccountHandler>();

        // Validators
        services.AddTransient<IValidator<CreateAccountingBookRequest>, CreateAccountingBookValidator>();
        services.AddTransient<IValidator<ListAccountingBooksRequest>, ListAccountingBooksValidator>();
        services.AddTransient<IValidator<CreateFiscalYearRequest>, CreateFiscalYearValidator>();
        services.AddTransient<IValidator<UpdateFiscalYearRequest>, UpdateFiscalYearValidator>();
        services.AddTransient<IValidator<ListFiscalYearsRequest>, ListFiscalYearsValidator>();
        services.AddTransient<IValidator<ResolveFiscalYearRequest>, ResolveFiscalYearValidator>();
        services.AddTransient<IValidator<FinalizeFiscalYearRequest>, FinalizeFiscalYearValidator>();
        services.AddTransient<IValidator<CancelFiscalYearRequest>, CancelFiscalYearValidator>();
        services.AddTransient<IValidator<ChartOfAccounts.UseCases.CreateAccountClass.CreateAccountClassRequest>, ChartOfAccounts.UseCases.CreateAccountClass.CreateAccountClassValidator>();
        services.AddTransient<IValidator<ChartOfAccounts.UseCases.UpdateAccountClass.UpdateAccountClassRequest>, ChartOfAccounts.UseCases.UpdateAccountClass.UpdateAccountClassValidator>();
        services.AddTransient<IValidator<ChartOfAccounts.UseCases.CreateGeneralAccount.CreateGeneralAccountRequest>, ChartOfAccounts.UseCases.CreateGeneralAccount.CreateGeneralAccountValidator>();
        services.AddTransient<IValidator<ChartOfAccounts.UseCases.UpdateGeneralAccount.UpdateGeneralAccountRequest>, ChartOfAccounts.UseCases.UpdateGeneralAccount.UpdateGeneralAccountValidator>();
        services.AddTransient<IValidator<ChartOfAccounts.UseCases.CreateSubsidiaryAccount.CreateSubsidiaryAccountRequest>, ChartOfAccounts.UseCases.CreateSubsidiaryAccount.CreateSubsidiaryAccountValidator>();
        services.AddTransient<IValidator<ChartOfAccounts.UseCases.UpdateSubsidiaryAccount.UpdateSubsidiaryAccountRequest>, ChartOfAccounts.UseCases.UpdateSubsidiaryAccount.UpdateSubsidiaryAccountValidator>();
        services.AddTransient<IValidator<ChartOfAccounts.UseCases.SearchAccounts.SearchAccountsRequest>, ChartOfAccounts.UseCases.SearchAccounts.SearchAccountsValidator>();
        services.AddTransient<IValidator<DetailAccounts.UseCases.CreateDetailAccount.CreateDetailAccountRequest>, DetailAccounts.UseCases.CreateDetailAccount.CreateDetailAccountValidator>();
        services.AddTransient<IValidator<DetailAccounts.UseCases.UpdateDetailAccount.UpdateDetailAccountRequest>, DetailAccounts.UseCases.UpdateDetailAccount.UpdateDetailAccountValidator>();
        services.AddTransient<IValidator<DetailAccounts.UseCases.ListDetailAccounts.ListDetailAccountsRequest>, DetailAccounts.UseCases.ListDetailAccounts.ListDetailAccountsValidator>();
        services.AddTransient<IValidator<DetailAccounts.UseCases.SearchDetailAccountsForPosting.SearchDetailAccountsForPostingRequest>, DetailAccounts.UseCases.SearchDetailAccountsForPosting.SearchDetailAccountsForPostingValidator>();

        return services;
    }
}
