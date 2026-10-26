using FAInances.Services.Entities;
using FAInances.Services.Exceptions;
using FAInances.Services.Models;
using FAInances.Services.Services;
using FAInances.Services.Tests.Infrastructure;
using MongoDB.Driver;

namespace FAInances.Services.Tests;

public class CategoryServiceTests(MongoDbFixture fixture) : MongoDbTestBase(fixture)
{
    private readonly string _userId = NewId();

    private CategoryService CreateService() => new(Db);

    [Fact]
    public async Task CreateAsync_StoresTrimmedCategory()
    {
        var service = CreateService();

        var category = await service.CreateAsync(_userId, new CategoryInput("  Groceries ", "  Food and drinks "), Ct);

        var stored = await Db.Categories.Find(c => c.Id == category.Id).SingleAsync(Ct);
        Assert.Equal(_userId, stored.UserId);
        Assert.Equal("Groceries", stored.Name);
        Assert.Equal("GROCERIES", stored.NormalizedName);
        Assert.Equal("Food and drinks", stored.Description);
    }

    [Fact]
    public async Task CreateAsync_StoresBlankDescriptionAsNull()
    {
        var category = await CreateService().CreateAsync(_userId, new CategoryInput("Rent", "   "), Ct);

        Assert.Null(category.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_RequiresName(string name)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().CreateAsync(_userId, new CategoryInput(name, null), Ct));
    }

    [Fact]
    public async Task CreateAsync_RejectsTooLongNameAndDescription()
    {
        var service = CreateService();
        var longName = new string('a', CategoryService.MaxNameLength + 1);
        var longDescription = new string('a', CategoryService.MaxDescriptionLength + 1);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateAsync(_userId, new CategoryInput(longName, null), Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateAsync(_userId, new CategoryInput("Rent", longDescription), Ct));
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateNameIgnoringCase()
    {
        var service = CreateService();
        await service.CreateAsync(_userId, new CategoryInput("Groceries", null), Ct);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateAsync(_userId, new CategoryInput("groceries", null), Ct));
    }

    [Fact]
    public async Task CreateAsync_AllowsSameNameForDifferentUsers()
    {
        var service = CreateService();
        await service.CreateAsync(_userId, new CategoryInput("Groceries", null), Ct);

        var other = await service.CreateAsync(NewId(), new CategoryInput("Groceries", null), Ct);

        Assert.Equal("Groceries", other.Name);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyTheUsersCategoriesSortedByName()
    {
        var service = CreateService();
        await service.CreateAsync(_userId, new CategoryInput("salary", null), Ct);
        await service.CreateAsync(_userId, new CategoryInput("Groceries", null), Ct);
        await service.CreateAsync(_userId, new CategoryInput("Rent", null), Ct);
        await service.CreateAsync(NewId(), new CategoryInput("Another user's", null), Ct);

        var categories = await service.GetAllAsync(_userId, Ct);

        Assert.Equal(["Groceries", "Rent", "salary"], categories.Select(c => c.Name));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCategory()
    {
        var service = CreateService();
        var created = await service.CreateAsync(_userId, new CategoryInput("Rent", null), Ct);

        var category = await service.GetByIdAsync(_userId, created.Id, Ct);

        Assert.Equal("Rent", category.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsNotFoundForMalformedUnknownOrForeignId()
    {
        var service = CreateService();
        var foreign = await service.CreateAsync(NewId(), new CategoryInput("Rent", null), Ct);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(_userId, "not-an-id", Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(_userId, NewId(), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(_userId, foreign.Id, Ct));
    }

    [Fact]
    public async Task UpdateAsync_ChangesNameAndDescription()
    {
        var service = CreateService();
        var created = await service.CreateAsync(_userId, new CategoryInput("Rent", null), Ct);

        await service.UpdateAsync(_userId, created.Id, new CategoryInput("Housing", "Rent and utilities"), Ct);

        var stored = await Db.Categories.Find(c => c.Id == created.Id).SingleAsync(Ct);
        Assert.Equal("Housing", stored.Name);
        Assert.Equal("HOUSING", stored.NormalizedName);
        Assert.Equal("Rent and utilities", stored.Description);
    }

    [Fact]
    public async Task UpdateAsync_AllowsChangingCaseOfOwnName()
    {
        var service = CreateService();
        var created = await service.CreateAsync(_userId, new CategoryInput("rent", null), Ct);

        var updated = await service.UpdateAsync(_userId, created.Id, new CategoryInput("Rent", null), Ct);

        Assert.Equal("Rent", updated.Name);
    }

    [Fact]
    public async Task UpdateAsync_RejectsNameOfAnotherCategory()
    {
        var service = CreateService();
        await service.CreateAsync(_userId, new CategoryInput("Rent", null), Ct);
        var groceries = await service.CreateAsync(_userId, new CategoryInput("Groceries", null), Ct);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateAsync(_userId, groceries.Id, new CategoryInput("RENT", null), Ct));
    }

    [Fact]
    public async Task DeleteAsync_RemovesUnusedCategory()
    {
        var service = CreateService();
        var created = await service.CreateAsync(_userId, new CategoryInput("Rent", null), Ct);

        await service.DeleteAsync(_userId, created.Id, Ct);

        Assert.False(await Db.Categories.Find(c => c.Id == created.Id).AnyAsync(Ct));
    }

    [Fact]
    public async Task DeleteAsync_RejectsCategoryInUse()
    {
        var service = CreateService();
        var created = await service.CreateAsync(_userId, new CategoryInput("Rent", null), Ct);
        await Db.Transactions.InsertOneAsync(
            new Transaction
            {
                UserId = _userId,
                CategoryId = created.Id,
                Type = TransactionType.Expense,
                Amount = 800m,
                Date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            cancellationToken: Ct);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(_userId, created.Id, Ct));

        Assert.Contains("1 transaction(s)", ex.Message);
        Assert.True(await Db.Categories.Find(c => c.Id == created.Id).AnyAsync(Ct));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsNotFoundForAnotherUsersCategory()
    {
        var service = CreateService();
        var foreign = await service.CreateAsync(NewId(), new CategoryInput("Rent", null), Ct);

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(_userId, foreign.Id, Ct));
        Assert.True(await Db.Categories.Find(c => c.Id == foreign.Id).AnyAsync(Ct));
    }
}
