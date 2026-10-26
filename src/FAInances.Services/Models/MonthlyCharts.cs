namespace FAInances.Services.Models;

public record CategoryTotal(string CategoryName, decimal Amount);

public record DailyTotal(int Day, decimal Income, decimal Expenses);

/// <summary>Data behind the month's charts: totals per category (per type) and per day.</summary>
public record MonthlyCharts(
    IReadOnlyList<CategoryTotal> ExpensesByCategory,
    IReadOnlyList<CategoryTotal> IncomeByCategory,
    IReadOnlyList<DailyTotal> Daily);
