using Application.RecurringPayments;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

public sealed record CreateRecurringPaymentRequest(
    string Name,
    string? Description,
    Guid AccountId,
    Guid? CategoryId,
    TransactionType Type,
    decimal Amount,
    string CurrencyCode,
    string Frequency,
    DateTime FirstExecutionDate,
    DateTime? EndDate);

public sealed record UpdateRecurringPaymentRequest(
    string Name,
    string? Description,
    Guid AccountId,
    Guid? CategoryId,
    TransactionType Type,
    decimal Amount,
    string CurrencyCode,
    string Frequency,
    DateTime FirstExecutionDate,
    DateTime? EndDate,
    bool IsActive);

public sealed record SetRecurringPaymentActiveRequest(bool IsActive);

[ApiController]
[Route("api/recurring-payments")]
[Authorize]
public sealed class RecurringPaymentsController : ControllerBase
{
    private readonly RecurringPaymentService _recurringPaymentService;

    public RecurringPaymentsController(RecurringPaymentService recurringPaymentService)
    {
        _recurringPaymentService = recurringPaymentService;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _recurringPaymentService.GetListAsync(ct);
        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRecurringPaymentRequest request, CancellationToken ct)
    {
        var result = await _recurringPaymentService.CreateAsync(
            request.Name,
            request.Description,
            request.AccountId,
            request.CategoryId,
            request.Type,
            request.Amount,
            request.CurrencyCode,
            request.Frequency,
            request.FirstExecutionDate,
            request.EndDate,
            ct);

        return this.ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRecurringPaymentRequest request, CancellationToken ct)
    {
        var result = await _recurringPaymentService.UpdateAsync(
            id,
            request.Name,
            request.Description,
            request.AccountId,
            request.CategoryId,
            request.Type,
            request.Amount,
            request.CurrencyCode,
            request.Frequency,
            request.FirstExecutionDate,
            request.EndDate,
            request.IsActive,
            ct);

        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetRecurringPaymentActiveRequest request, CancellationToken ct)
    {
        var result = await _recurringPaymentService.SetActiveAsync(id, request.IsActive, ct);
        return this.ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _recurringPaymentService.DeleteAsync(id, ct);
        return this.ToActionResult(result);
    }
}
