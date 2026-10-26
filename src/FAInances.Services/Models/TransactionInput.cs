using FAInances.Services.Entities;

namespace FAInances.Services.Models;

public record TransactionInput(
    TransactionType Type,
    decimal Amount,
    DateTime Date,
    string CategoryId,
    string? Description);
