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

        // Validators
        services.AddTransient<IValidator<CreateAccountingBookRequest>, CreateAccountingBookValidator>();
        services.AddTransient<IValidator<ListAccountingBooksRequest>, ListAccountingBooksValidator>();
        services.AddTransient<IValidator<CreateFiscalYearRequest>, CreateFiscalYearValidator>();
        services.AddTransient<IValidator<UpdateFiscalYearRequest>, UpdateFiscalYearValidator>();
        services.AddTransient<IValidator<ListFiscalYearsRequest>, ListFiscalYearsValidator>();
        services.AddTransient<IValidator<ResolveFiscalYearRequest>, ResolveFiscalYearValidator>();
        services.AddTransient<IValidator<FinalizeFiscalYearRequest>, FinalizeFiscalYearValidator>();
        services.AddTransient<IValidator<CancelFiscalYearRequest>, CancelFiscalYearValidator>();

        return services;
    }
}
