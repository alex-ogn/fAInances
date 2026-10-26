using FAInances.Services.Entities;

namespace FAInances.Web.Controllers.Api;

public record CategoryResponse(string Id, string Name, string? Description)
{
    public static CategoryResponse From(Category category) => new(category.Id, category.Name, category.Description);
}

public record TransactionResponse(
    string Id,
    TransactionType Type,
    decimal Amount,
    DateTime Date,
    string CategoryId,
    string? Description)
{
    public static TransactionResponse From(Transaction transaction) => new(
        transaction.Id,
        transaction.Type,
        transaction.Amount,
        transaction.Date,
        transaction.CategoryId,
        transaction.Description);
}
