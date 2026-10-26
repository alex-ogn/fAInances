namespace FAInances.Services.Models;

public record MonthlySummary(int Year, int Month, decimal TotalIncome, decimal TotalExpenses)
{
    public decimal Balance => TotalIncome - TotalExpenses;
}
