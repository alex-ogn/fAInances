using FAInances.Services.Data;
using FAInances.Services.Options;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace FAInances.Services.Tests.Infrastructure;

/// <summary>
/// Provides a MongoDB database shared by every test class in <see cref="MongoDbCollection"/>.
/// Starts a throwaway <c>mongo:7</c> container through Testcontainers (requires Docker), unless the
/// <c>FAINANCES_TEST_MONGODB</c> environment variable holds a connection string to an existing server.
/// Each fixture works in its own uniquely named database, which is dropped when the run ends.
/// </summary>
public sealed class MongoDbFixture : IAsyncLifetime
{
    public const string ConnectionStringVariable = "FAINANCES_TEST_MONGODB";

    private MongoDbContainer? _container;
    private IMongoClient? _client;
    private MongoDbContext? _db;

    public string DatabaseName { get; } = $"fainances_tests_{Guid.NewGuid():N}";

    public MongoDbContext Db => _db ?? throw new InvalidOperationException("The fixture has not been initialized.");

    public async ValueTask InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _container = new MongoDbBuilder("mongo:7").Build();
            await _container.StartAsync();
            connectionString = _container.GetConnectionString();
        }

        _client = new MongoClient(connectionString);
        _db = new MongoDbContext(
            _client,
            Microsoft.Extensions.Options.Options.Create(new MongoDbOptions
            {
                ConnectionString = connectionString,
                DatabaseName = DatabaseName
            }));

        await _db.EnsureIndexesAsync();
    }

    /// <summary>Removes all documents but keeps the collections and their indexes.</summary>
    public async Task ResetAsync()
    {
        await Db.Users.DeleteManyAsync(FilterDefinition<Entities.User>.Empty);
        await Db.Categories.DeleteManyAsync(FilterDefinition<Entities.Category>.Empty);
        await Db.Transactions.DeleteManyAsync(FilterDefinition<Entities.Transaction>.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DropDatabaseAsync(DatabaseName);
            _client.Dispose();
        }

        if (_container is not null)
            await _container.DisposeAsync();
    }
}
