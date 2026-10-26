namespace FAInances.Services.Tests.Infrastructure;

/// <summary>
/// Test classes marked with <c>[Collection(MongoDbCollection.Name)]</c> share one <see cref="MongoDbFixture"/>
/// and run sequentially, so they never see each other's data mid-test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class MongoDbCollection : ICollectionFixture<MongoDbFixture>
{
    public const string Name = "MongoDb";
}
