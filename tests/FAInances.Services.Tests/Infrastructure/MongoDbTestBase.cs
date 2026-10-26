using FAInances.Services.Data;
using MongoDB.Bson;

namespace FAInances.Services.Tests.Infrastructure;

/// <summary>Base class for tests that hit MongoDB; every test starts from empty collections.</summary>
[Collection(MongoDbCollection.Name)]
public abstract class MongoDbTestBase(MongoDbFixture fixture) : IAsyncLifetime
{
    protected MongoDbContext Db => fixture.Db;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected static string NewId() => ObjectId.GenerateNewId().ToString();

    public virtual async ValueTask InitializeAsync() => await fixture.ResetAsync();

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
