using FAInances.Services.Entities;

namespace FAInances.Services.Models;

public record TransactionListItem(
    string Id,
    TransactionType Type,
    decimal Amount,
    DateTime Date,
    string CategoryId,
    string CategoryName,
    string? Description);
