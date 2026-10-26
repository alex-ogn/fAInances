using FAInances.Services.Entities;
using FAInances.Services.Options;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace FAInances.Services.Data;

/// <summary>Gives services access to the MongoDB collections and sets up indexes.</summary>
public class MongoDbContext
{
    public MongoDbContext(IMongoClient client, IOptions<MongoDbOptions> options)
    {
        Database = client.GetDatabase(options.Value.DatabaseName);
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<User> Users => Database.GetCollection<User>("users");

    public IMongoCollection<Category> Categories => Database.GetCollection<Category>("categories");

    public IMongoCollection<Transaction> Transactions => Database.GetCollection<Transaction>("transactions");

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Users.Indexes.CreateOneAsync(
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);

        await Categories.Indexes.CreateOneAsync(
            new CreateIndexModel<Category>(
                Builders<Category>.IndexKeys.Ascending(c => c.UserId).Ascending(c => c.NormalizedName),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);

        await Transactions.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<Transaction>(
                    Builders<Transaction>.IndexKeys.Ascending(t => t.UserId).Descending(t => t.Date)),
                new CreateIndexModel<Transaction>(
                    Builders<Transaction>.IndexKeys.Ascending(t => t.UserId).Ascending(t => t.CategoryId))
            ],
            cancellationToken);
    }
}
