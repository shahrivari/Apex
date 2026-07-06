namespace Apex.IntegrationTests.Infrastructure.Data;

[CollectionDefinition(Name)]
public sealed class SeparatedReadWriteIntegrationTestCollection
    : ICollectionFixture<SeparatedReadWriteIntegrationTestFixture>
{
    public const string Name = "SeparatedReadWriteIntegrationTestCollection";
}
