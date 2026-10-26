using EphemeralMongo;
using FAInances.Services.Data;
using FAInances.Services.Options;
using MongoDB.Driver;

namespace FAInances.Services.Tests.Infrastructure;

/// <summary>
/// Provides a MongoDB database shared by every test class in <see cref="MongoDbCollection"/>.
/// Starts a throwaway MongoDB 7 server with EphemeralMongo (no Docker needed; the binaries are
/// downloaded on first use and cached), and stops it and deletes its data when the run ends.
/// </summary>
public sealed class MongoDbFixture : IAsyncLifetime
{
    private IMongoRunner? _runner;
    private IMongoClient? _client;
    private MongoDbContext? _db;

    public MongoDbContext Db => _db ?? throw new InvalidOperationException("The fixture has not been initialized.");

    public async ValueTask InitializeAsync()
    {
        _runner = await MongoRunner.RunAsync(new MongoRunnerOptions
        {
            Version = MongoVersion.V7,
            AdditionalArguments = ["--quiet"]
        });

        _client = new MongoClient(_runner.ConnectionString);
        _db = new MongoDbContext(
            _client,
            Microsoft.Extensions.Options.Options.Create(new MongoDbOptions
            {
                ConnectionString = _runner.ConnectionString,
                DatabaseName = "fainances_tests"
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

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _runner?.Dispose();
        return ValueTask.CompletedTask;
    }
}
