using FAInances.Services.Entities;
using FAInances.Services.Exceptions;
using FAInances.Services.Models;
using FAInances.Services.Services;
using FAInances.Services.Tests.Infrastructure;
using MongoDB.Driver;

namespace FAInances.Services.Tests;

public class TransactionServiceTests(MongoDbFixture fixture) : MongoDbTestBase(fixture)
{
    private readonly string _userId = NewId();
    private Category _groceries = null!;
    private Category _salary = null!;

    private CategoryService CategoryService => new(Db);

    private TransactionService CreateService() => new(Db, CategoryService);

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _groceries = await CategoryService.CreateAsync(_userId, new CategoryInput("Groceries", null), Ct);
        _salary = await CategoryService.CreateAsync(_userId, new CategoryInput("Salary", null), Ct);
    }

    private static DateTime Day(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private TransactionInput Expense(decimal amount, DateTime date, string? description = null) =>
        new(TransactionType.Expense, amount, date, _groceries.Id, description);

    private TransactionInput Income(decimal amount, DateTime date) =>
        new(TransactionType.Income, amount, date, _salary.Id, null);

    [Fact]
    public async Task CreateAsync_StoresTransactionWithDateOnlyInUtc()
    {
        var service = CreateService();
        var localDateTime = new DateTime(2026, 3, 15, 18, 45, 0, DateTimeKind.Local);

        var created = await service.CreateAsync(_userId, Expense(12.34m, localDateTime, "  Weekly shop "), Ct);

        var stored = await Db.Transactions.Find(t => t.Id == created.Id).SingleAsync(Ct);
        Assert.Equal(_userId, stored.UserId);
        Assert.Equal(TransactionType.Expense, stored.Type);
        Assert.Equal(12.34m, stored.Amount);
        Assert.Equal(Day(2026, 3, 15), stored.Date);
        Assert.Equal(DateTimeKind.Utc, stored.Date.Kind);
        Assert.Equal(_groceries.Id, stored.CategoryId);
        Assert.Equal("Weekly shop", stored.Description);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1.234")]
    [InlineData("1000000000.01")]
    public async Task CreateAsync_RejectsInvalidAmount(string amount)
    {
        var input = Expense(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), Day(2026, 3, 1));

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().CreateAsync(_userId, input, Ct));
        Assert.Equal(0, await Db.Transactions.CountDocumentsAsync(FilterDefinition<Transaction>.Empty, cancellationToken: Ct));
    }

    [Fact]
    public async Task CreateAsync_AcceptsMaxAmount()
    {
        var created = await CreateService().CreateAsync(_userId, Expense(TransactionService.MaxAmount, Day(2026, 3, 1)), Ct);

        Assert.Equal(TransactionService.MaxAmount, created.Amount);
    }

    [Fact]
    public async Task CreateAsync_RejectsUndefinedTypeMissingDateAndTooLongDescription()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(
            _userId, Expense(10m, Day(2026, 3, 1)) with { Type = (TransactionType)42 }, Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(
            _userId, Expense(10m, default), Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(
            _userId, Expense(10m, Day(2026, 3, 1), new string('a', TransactionService.MaxDescriptionLength + 1)), Ct));
    }

    [Fact]
    public async Task CreateAsync_RejectsMissingUnknownOrForeignCategory()
    {
        var service = CreateService();
        var foreignCategory = await CategoryService.CreateAsync(NewId(), new CategoryInput("Groceries", null), Ct);

        foreach (var categoryId in new[] { "", "not-an-id", NewId(), foreignCategory.Id })
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(
                _userId, Expense(10m, Day(2026, 3, 1)) with { CategoryId = categoryId }, Ct));
        }
    }

    [Fact]
    public async Task GetForMonthAsync_ReturnsUsersTransactionsInMonthNewestFirstWithCategoryNames()
    {
        var service = CreateService();
        var first = await service.CreateAsync(_userId, Expense(10m, Day(2026, 3, 1)), Ct);
        var last = await service.CreateAsync(_userId, Income(2000m, Day(2026, 3, 31)), Ct);
        var middle = await service.CreateAsync(_userId, Expense(20m, Day(2026, 3, 15)), Ct);
        await service.CreateAsync(_userId, Expense(30m, Day(2026, 2, 28)), Ct);
        await service.CreateAsync(_userId, Expense(40m, Day(2026, 4, 1)), Ct);
        var otherUserId = NewId();
        var otherCategory = await CategoryService.CreateAsync(otherUserId, new CategoryInput("Other", null), Ct);
        await service.CreateAsync(otherUserId, new TransactionInput(TransactionType.Expense, 50m, Day(2026, 3, 10), otherCategory.Id, null), Ct);

        var items = await service.GetForMonthAsync(_userId, 2026, 3, Ct);

        Assert.Equal([last.Id, middle.Id, first.Id], items.Select(i => i.Id));
        Assert.Equal(["Salary", "Groceries", "Groceries"], items.Select(i => i.CategoryName));
    }

    [Fact]
    public async Task GetForMonthAsync_LabelsTransactionsWhoseCategoryNoLongerExists()
    {
        var service = CreateService();
        await service.CreateAsync(_userId, Expense(10m, Day(2026, 3, 1)), Ct);
        await Db.Categories.DeleteOneAsync(c => c.Id == _groceries.Id, Ct);

        var item = Assert.Single(await service.GetForMonthAsync(_userId, 2026, 3, Ct));

        Assert.Equal("(deleted category)", item.CategoryName);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_SumsIncomeAndExpensesForTheMonth()
    {
        var service = CreateService();
        await service.CreateAsync(_userId, Income(2500m, Day(2026, 3, 1)), Ct);
        await service.CreateAsync(_userId, Expense(100.25m, Day(2026, 3, 2)), Ct);
        await service.CreateAsync(_userId, Expense(49.75m, Day(2026, 3, 31)), Ct);
        await service.CreateAsync(_userId, Income(999m, Day(2026, 4, 1)), Ct);

        var summary = await service.GetMonthlySummaryAsync(_userId, 2026, 3, Ct);

        Assert.Equal(new MonthlySummary(2026, 3, 2500m, 150m), summary);
        Assert.Equal(2350m, summary.Balance);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_ReturnsZerosForEmptyMonth()
    {
        var summary = await CreateService().GetMonthlySummaryAsync(_userId, 2026, 3, Ct);

        Assert.Equal(new MonthlySummary(2026, 3, 0m, 0m), summary);
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(1899, 1)]
    public async Task MonthQueries_RejectInvalidMonth(int year, int month)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetForMonthAsync(_userId, year, month, Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetMonthlySummaryAsync(_userId, year, month, Ct));
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsNotFoundForMalformedUnknownOrForeignId()
    {
        var service = CreateService();
        var created = await service.CreateAsync(_userId, Expense(10m, Day(2026, 3, 1)), Ct);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(_userId, "not-an-id", Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(_userId, NewId(), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(NewId(), created.Id, Ct));
    }

    [Fact]
    public async Task UpdateAsync_ReplacesTransactionFields()
    {
        var service = CreateService();
        var created = await service.CreateAsync(_userId, Expense(10m, Day(2026, 3, 1), "Shop"), Ct);

        await service.UpdateAsync(_userId, created.Id, Income(2000m, Day(2026, 3, 5)), Ct);

        var stored = await Db.Transactions.Find(t => t.Id == created.Id).SingleAsync(Ct);
        Assert.Equal(TransactionType.Income, stored.Type);
        Assert.Equal(2000m, stored.Amount);
        Assert.Equal(Day(2026, 3, 5), stored.Date);
        Assert.Equal(_salary.Id, stored.CategoryId);
        Assert.Null(stored.Description);
    }

    [Fact]
    public async Task UpdateAsync_LeavesTransactionUnchangedWhenInputIsInvalid()
    {
        var service = CreateService();
        var created = await service.CreateAsync(_userId, Expense(10m, Day(2026, 3, 1)), Ct);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateAsync(_userId, created.Id, Expense(-1m, Day(2026, 3, 1)), Ct));

        var stored = await Db.Transactions.Find(t => t.Id == created.Id).SingleAsync(Ct);
        Assert.Equal(10m, stored.Amount);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTransaction()
    {
        var service = CreateService();
        var created = await service.CreateAsync(_userId, Expense(10m, Day(2026, 3, 1)), Ct);

        await service.DeleteAsync(_userId, created.Id, Ct);

        Assert.False(await Db.Transactions.Find(t => t.Id == created.Id).AnyAsync(Ct));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsNotFoundForAnotherUsersTransaction()
    {
        var service = CreateService();
        var created = await service.CreateAsync(_userId, Expense(10m, Day(2026, 3, 1)), Ct);

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(NewId(), created.Id, Ct));
        Assert.True(await Db.Transactions.Find(t => t.Id == created.Id).AnyAsync(Ct));
    }
}
