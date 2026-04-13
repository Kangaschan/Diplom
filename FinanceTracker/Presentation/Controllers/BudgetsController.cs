using Application.Budgets;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

public sealed record CreateBudgetRequest(Guid CategoryId, Guid? AccountId, decimal LimitAmount, string CurrencyCode, BudgetPeriodType PeriodType, DateTime StartDate, DateTime EndDate);
public sealed record UpdateBudgetRequest(decimal LimitAmount, DateTime StartDate, DateTime EndDate);

[ApiController]
[Route("api/budgets")]
[Authorize]
public sealed class BudgetsController(BudgetService budgetService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await budgetService.GetListAsync(ct);
        return this.ToActionResult(result);
    }

    [HttpGet("usage")]
    public async Task<IActionResult> Usage(CancellationToken ct)
    {
        var result = await budgetService.GetUsageAsync(ct);
        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBudgetRequest request, CancellationToken ct)
    {
        var result = await budgetService.CreateAsync(
            request.CategoryId,
            request.AccountId,
            request.LimitAmount,
            request.CurrencyCode,
            request.PeriodType,
            request.StartDate,
            request.EndDate,
            ct);
        return this.ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBudgetRequest request, CancellationToken ct)
    {
        var result = await budgetService.UpdateAsync(id, request.LimitAmount, request.StartDate, request.EndDate, ct);
        return this.ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await budgetService.DeleteAsync(id, ct);
        return this.ToActionResult(result);
    }
}
