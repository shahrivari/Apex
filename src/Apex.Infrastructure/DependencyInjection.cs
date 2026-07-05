namespace Apex.Infrastructure;

using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Ids;
using Apex.Application.Abstractions.Time;
using Apex.Infrastructure.Data;
using Apex.Infrastructure.Ids;
using Apex.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IModuleDatabaseResolver, ModuleDatabaseResolver>();
        services.AddSingleton<IShardResolver, DefaultShardResolver>();

        services.AddSingleton<IReadDbConnectionFactory, SqlReadDbConnectionFactory>();
        services.AddScoped<IWriteDbConnectionFactory, SqlWriteDbConnectionFactory>();
        services.AddScoped<IWriteDbSession, SqlWriteDbSession>();
        services.AddScoped<IWriteTransactionRunner, SqlWriteTransactionRunner>();

        services.AddSingleton<IIdGenerator, TsidIdGenerator>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
