namespace Apex.Modules.Accounting;

using Apex.Modules.Accounting.AccountingBooks.UseCases.ActivateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ArchiveAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.GetAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ListAccountingBooks;
using Apex.Modules.Accounting.AccountingBooks.Repositories;
using Apex.Modules.Accounting.AccountingBooks.UseCases.SuspendAccountingBook;
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

        // Handlers
        services.AddTransient<CreateAccountingBookHandler>();
        services.AddTransient<GetAccountingBookHandler>();
        services.AddTransient<ListAccountingBooksHandler>();
        services.AddTransient<ActivateAccountingBookHandler>();
        services.AddTransient<SuspendAccountingBookHandler>();
        services.AddTransient<ArchiveAccountingBookHandler>();

        // Validators
        services.AddTransient<IValidator<CreateAccountingBookRequest>, CreateAccountingBookValidator>();
        services.AddTransient<IValidator<ListAccountingBooksRequest>, ListAccountingBooksValidator>();

        return services;
    }
}
