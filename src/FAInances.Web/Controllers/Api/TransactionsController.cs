using FAInances.Services.Models;
using FAInances.Services.Services;
using FAInances.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FAInances.Web.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/transactions")]
public class TransactionsController(ITransactionService transactionService) : ControllerBase
{
    /// <summary>Transactions for a month; defaults to the current month.</summary>
    [HttpGet]
    public Task<IReadOnlyList<TransactionListItem>> GetForMonth(int? year, int? month, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        return transactionService.GetForMonthAsync(User.GetUserId(), year ?? today.Year, month ?? today.Month, cancellationToken);
    }

    [HttpGet("summary")]
    public Task<MonthlySummary> GetSummary(int? year, int? month, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        return transactionService.GetMonthlySummaryAsync(User.GetUserId(), year ?? today.Year, month ?? today.Month, cancellationToken);
    }

    [HttpGet("{id}")]
    public async Task<TransactionResponse> Get(string id, CancellationToken cancellationToken) =>
        TransactionResponse.From(await transactionService.GetByIdAsync(User.GetUserId(), id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create(TransactionInput input, CancellationToken cancellationToken)
    {
        var transaction = await transactionService.CreateAsync(User.GetUserId(), input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = transaction.Id }, TransactionResponse.From(transaction));
    }

    [HttpPut("{id}")]
    public async Task<TransactionResponse> Update(string id, TransactionInput input, CancellationToken cancellationToken) =>
        TransactionResponse.From(await transactionService.UpdateAsync(User.GetUserId(), id, input, cancellationToken));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await transactionService.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
