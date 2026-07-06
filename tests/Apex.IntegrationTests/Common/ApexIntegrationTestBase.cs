namespace Apex.IntegrationTests.Common;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

[Collection(ApexIntegrationTestCollection.Name)]
public abstract class ApexIntegrationTestBase
{
    protected ApexIntegrationTestBase(ApexIntegrationTestFixture fixture)
    {
        Fixture = fixture;
    }

    protected ApexIntegrationTestFixture Fixture { get; }

    protected ServiceProvider CreateServiceProvider()
    {
        return Fixture.CreateServiceProvider();
    }

    protected async Task<ServiceScopeHandle> CreateScopeAsync()
    {
        var provider = CreateServiceProvider();
        var scope = provider.CreateAsyncScope();

        return new ServiceScopeHandle(provider, scope);
    }

    protected SqlConnection CreateAccountingConnection()
    {
        return Fixture.CreateAccountingConnection();
    }

    protected Task ResetAccountingDatabaseAsync()
    {
        return Fixture.ResetAccountingDatabaseAsync();
    }

    protected sealed class ServiceScopeHandle : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;

        public ServiceScopeHandle(ServiceProvider provider, AsyncServiceScope scope)
        {
            _provider = provider;
            _scope = scope;
        }

        public IServiceProvider Services => _scope.ServiceProvider;

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
