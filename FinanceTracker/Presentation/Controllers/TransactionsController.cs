using Application.Transactions;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

public sealed record CreateTransactionRequest(
    Guid AccountId,
    Guid? CategoryId,
    TransactionType Type,
    decimal Amount,
    string CurrencyCode,
    decimal? ManualRate,
    DateTime TransactionDate,
    string? Description,
    TransactionSource Source = TransactionSource.Manual);

public sealed record UpdateTransactionRequest(
    Guid AccountId,
    Guid? CategoryId,
    decimal Amount,
    string CurrencyCode,
    decimal? ManualRate,
    DateTime TransactionDate,
    string? Description);

[ApiController]
[Route("api/transactions")]
[Authorize]
public sealed class TransactionsController(TransactionService transactionService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? accountId,
        [FromQuery] Guid? categoryId,
        [FromQuery] TransactionType? type,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var result = await transactionService.GetListAsync(from, to, accountId, categoryId, type, search, ct);
        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest request, CancellationToken ct)
    {
        var result = await transactionService.CreateAsync(
            request.AccountId,
            request.CategoryId,
            request.Type,
            request.Amount,
            request.CurrencyCode,
            request.ManualRate,
            request.TransactionDate,
            request.Description,
            request.Source,
            ct);
        return this.ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTransactionRequest request, CancellationToken ct)
    {
        var result = await transactionService.UpdateAsync(
            id,
            request.AccountId,
            request.CategoryId,
            request.Amount,
            request.CurrencyCode,
            request.ManualRate,
            request.Description,
            request.TransactionDate,
            ct);
        return this.ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await transactionService.DeleteAsync(id, ct);
        return this.ToActionResult(result);
    }
}
