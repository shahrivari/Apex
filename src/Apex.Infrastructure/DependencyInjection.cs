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
        DapperDateOnlyTypeHandler.Register();

        services.AddSingleton<SqlShardDirectory>();
        services.AddSingleton<IShardDirectory>(provider =>
            provider.GetRequiredService<SqlShardDirectory>());
        services.AddSingleton<IShardResolver, DefaultShardResolver>();
        services.AddSingleton<SqlShardConnectionFactory>();
        services.AddSingleton<IShardConnectionFactory>(provider =>
            provider.GetRequiredService<SqlShardConnectionFactory>());
        services.AddScoped<IGeneralConnectionFactory, SqlGeneralConnectionFactory>();
        services.AddScoped<IGeneralTransactionRunner, SqlGeneralTransactionRunner>();

        services.AddSingleton<IIdGenerator, TsidIdGenerator>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
