namespace Apex.IntegrationTests.Http;

[CollectionDefinition(Name)]
public sealed class ApexHttpIntegrationTestCollection : ICollectionFixture<ApexWebApplicationFactory>
{
    public const string Name = "ApexHttpIntegrationTestCollection";
}
