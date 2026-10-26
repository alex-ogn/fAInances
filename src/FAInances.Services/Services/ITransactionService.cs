using FAInances.Services.Entities;
using FAInances.Services.Models;

namespace FAInances.Services.Services;

public interface ITransactionService
{
    /// <summary>Transactions dated within the given month, newest first, with category names resolved.</summary>
    Task<IReadOnlyList<TransactionListItem>> GetForMonthAsync(string userId, int year, int month, CancellationToken cancellationToken = default);

    Task<MonthlySummary> GetMonthlySummaryAsync(string userId, int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Per-category totals (largest first, the tail folded into "Other") for expenses and income,
    /// plus income/expense totals for every day of the month.
    /// </summary>
    Task<MonthlyCharts> GetMonthlyChartsAsync(string userId, int year, int month, CancellationToken cancellationToken = default);

    Task<Transaction> GetByIdAsync(string userId, string transactionId, CancellationToken cancellationToken = default);

    Task<Transaction> CreateAsync(string userId, TransactionInput input, CancellationToken cancellationToken = default);

    Task<Transaction> UpdateAsync(string userId, string transactionId, TransactionInput input, CancellationToken cancellationToken = default);

    Task DeleteAsync(string userId, string transactionId, CancellationToken cancellationToken = default);
}
