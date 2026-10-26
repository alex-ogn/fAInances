using FAInances.Services.Data;
using FAInances.Services.Entities;
using FAInances.Services.Exceptions;
using FAInances.Services.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FAInances.Services.Services;

public class CategoryService(MongoDbContext db) : ICategoryService
{
    public const int MaxNameLength = 50;
    public const int MaxDescriptionLength = 200;

    public async Task<IReadOnlyList<Category>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await db.Categories
            .Find(c => c.UserId == userId)
            .SortBy(c => c.NormalizedName)
            .ToListAsync(cancellationToken);
    }

    public async Task<Category> GetByIdAsync(string userId, string categoryId, CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(categoryId, out _))
            throw new NotFoundException("Category not found.");

        return await db.Categories
            .Find(c => c.Id == categoryId && c.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Category not found.");
    }

    public async Task<Category> CreateAsync(string userId, CategoryInput input, CancellationToken cancellationToken = default)
    {
        var (name, description) = Validate(input);
        var normalizedName = Normalize(name);

        await EnsureNameIsFreeAsync(userId, name, exceptId: null, cancellationToken);

        var category = new Category
        {
            UserId = userId,
            Name = name,
            NormalizedName = normalizedName,
            Description = description
        };

        try
        {
            await db.Categories.InsertOneAsync(category, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw DuplicateName(name);
        }

        return category;
    }

    public async Task<Category> UpdateAsync(string userId, string categoryId, CategoryInput input, CancellationToken cancellationToken = default)
    {
        var category = await GetByIdAsync(userId, categoryId, cancellationToken);
        var (name, description) = Validate(input);

        await EnsureNameIsFreeAsync(userId, name, exceptId: category.Id, cancellationToken);

        category.Name = name;
        category.NormalizedName = Normalize(name);
        category.Description = description;

        try
        {
            await db.Categories.ReplaceOneAsync(c => c.Id == category.Id, category, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw DuplicateName(name);
        }

        return category;
    }

    public async Task DeleteAsync(string userId, string categoryId, CancellationToken cancellationToken = default)
    {
        var category = await GetByIdAsync(userId, categoryId, cancellationToken);

        var usageCount = await db.Transactions.CountDocumentsAsync(
            t => t.UserId == userId && t.CategoryId == category.Id,
            cancellationToken: cancellationToken);
        if (usageCount > 0)
            throw new BusinessRuleException(
                $"\"{category.Name}\" is used by {usageCount} transaction(s) and cannot be deleted.");

        await db.Categories.DeleteOneAsync(c => c.Id == category.Id, cancellationToken);
    }

    private static (string Name, string? Description) Validate(CategoryInput input)
    {
        var name = input.Name?.Trim() ?? string.Empty;
        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();

        if (name.Length == 0)
            throw new BusinessRuleException("Category name is required.");
        if (name.Length > MaxNameLength)
            throw new BusinessRuleException($"Category name must be at most {MaxNameLength} characters.");
        if (description?.Length > MaxDescriptionLength)
            throw new BusinessRuleException($"Description must be at most {MaxDescriptionLength} characters.");

        return (name, description);
    }

    private async Task EnsureNameIsFreeAsync(string userId, string name, string? exceptId, CancellationToken cancellationToken)
    {
        var normalizedName = Normalize(name);
        var filter = Builders<Category>.Filter.Eq(c => c.UserId, userId)
                     & Builders<Category>.Filter.Eq(c => c.NormalizedName, normalizedName);
        if (exceptId is not null)
            filter &= Builders<Category>.Filter.Ne(c => c.Id, exceptId);

        if (await db.Categories.Find(filter).AnyAsync(cancellationToken))
            throw DuplicateName(name);
    }

    private static string Normalize(string name) => name.ToUpperInvariant();

    private static BusinessRuleException DuplicateName(string name) =>
        new($"A category named \"{name}\" already exists.");
}
