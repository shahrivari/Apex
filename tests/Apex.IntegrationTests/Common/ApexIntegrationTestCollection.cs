namespace Apex.IntegrationTests.Common;

[CollectionDefinition(Name)]
public sealed class ApexIntegrationTestCollection : ICollectionFixture<ApexIntegrationTestFixture>
{
    public const string Name = "ApexIntegrationTestCollection";
}
