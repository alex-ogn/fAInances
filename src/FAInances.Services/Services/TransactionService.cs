using FAInances.Services.Data;
using FAInances.Services.Entities;
using FAInances.Services.Exceptions;
using FAInances.Services.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FAInances.Services.Services;

public class TransactionService(MongoDbContext db, ICategoryService categoryService) : ITransactionService
{
    public const int MaxDescriptionLength = 200;
    public const decimal MaxAmount = 1_000_000_000m;

    public async Task<IReadOnlyList<TransactionListItem>> GetForMonthAsync(string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var (from, to) = MonthRange(year, month);

        var transactions = await db.Transactions
            .Find(t => t.UserId == userId && t.Date >= from && t.Date < to)
            .SortByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        var categoryIds = transactions.Select(t => t.CategoryId).Distinct().ToList();
        var categoryNames = await db.Categories
            .Find(c => c.UserId == userId && categoryIds.Contains(c.Id))
            .Project(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);
        var namesById = categoryNames.ToDictionary(c => c.Id, c => c.Name);

        return transactions
            .Select(t => new TransactionListItem(
                t.Id,
                t.Type,
                t.Amount,
                t.Date,
                t.CategoryId,
                namesById.GetValueOrDefault(t.CategoryId, "(deleted category)"),
                t.Description))
            .ToList();
    }

    public async Task<MonthlySummary> GetMonthlySummaryAsync(string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var (from, to) = MonthRange(year, month);

        var amounts = await db.Transactions
            .Find(t => t.UserId == userId && t.Date >= from && t.Date < to)
            .Project(t => new { t.Type, t.Amount })
            .ToListAsync(cancellationToken);

        return new MonthlySummary(
            year,
            month,
            TotalIncome: amounts.Where(a => a.Type == TransactionType.Income).Sum(a => a.Amount),
            TotalExpenses: amounts.Where(a => a.Type == TransactionType.Expense).Sum(a => a.Amount));
    }

    public async Task<Transaction> GetByIdAsync(string userId, string transactionId, CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(transactionId, out _))
            throw new NotFoundException("Transaction not found.");

        return await db.Transactions
            .Find(t => t.Id == transactionId && t.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Transaction not found.");
    }

    public async Task<Transaction> CreateAsync(string userId, TransactionInput input, CancellationToken cancellationToken = default)
    {
        var transaction = new Transaction { UserId = userId };
        await ApplyAsync(userId, transaction, input, cancellationToken);

        await db.Transactions.InsertOneAsync(transaction, cancellationToken: cancellationToken);
        return transaction;
    }

    public async Task<Transaction> UpdateAsync(string userId, string transactionId, TransactionInput input, CancellationToken cancellationToken = default)
    {
        var transaction = await GetByIdAsync(userId, transactionId, cancellationToken);
        await ApplyAsync(userId, transaction, input, cancellationToken);

        await db.Transactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction, cancellationToken: cancellationToken);
        return transaction;
    }

    public async Task DeleteAsync(string userId, string transactionId, CancellationToken cancellationToken = default)
    {
        var transaction = await GetByIdAsync(userId, transactionId, cancellationToken);
        await db.Transactions.DeleteOneAsync(t => t.Id == transaction.Id, cancellationToken);
    }

    /// <summary>Validates the input and copies it onto the transaction.</summary>
    private async Task ApplyAsync(string userId, Transaction transaction, TransactionInput input, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(input.Type))
            throw new BusinessRuleException("Choose whether this is an income or an expense.");
        if (input.Amount <= 0)
            throw new BusinessRuleException("Amount must be greater than zero.");
        if (input.Amount > MaxAmount)
            throw new BusinessRuleException($"Amount must be at most {MaxAmount:N0}.");
        if (decimal.Round(input.Amount, 2) != input.Amount)
            throw new BusinessRuleException("Amount can have at most two decimal places.");
        if (input.Date == default)
            throw new BusinessRuleException("Date is required.");

        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        if (description?.Length > MaxDescriptionLength)
            throw new BusinessRuleException($"Description must be at most {MaxDescriptionLength} characters.");

        if (string.IsNullOrWhiteSpace(input.CategoryId))
            throw new BusinessRuleException("Please choose a category.");

        Category category;
        try
        {
            category = await categoryService.GetByIdAsync(userId, input.CategoryId, cancellationToken);
        }
        catch (NotFoundException)
        {
            throw new BusinessRuleException("The selected category does not exist.");
        }

        transaction.Type = input.Type;
        transaction.Amount = input.Amount;
        transaction.Date = DateTime.SpecifyKind(input.Date.Date, DateTimeKind.Utc);
        transaction.CategoryId = category.Id;
        transaction.Description = description;
    }

    private static (DateTime From, DateTime To) MonthRange(int year, int month)
    {
        if (year is < 1900 or > 9999 || month is < 1 or > 12)
            throw new BusinessRuleException("Invalid month.");

        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (from, from.AddMonths(1));
    }
}
