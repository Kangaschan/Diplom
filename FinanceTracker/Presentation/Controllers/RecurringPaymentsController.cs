using Application.RecurringPayments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

public sealed record CreateRecurringPaymentRequest(string Name, decimal EstimatedAmount, string CurrencyCode, string Frequency);

[ApiController]
[Route("api/recurring-payments")]
[Authorize]
public sealed class RecurringPaymentsController(RecurringPaymentService recurringPaymentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await recurringPaymentService.GetListAsync(ct);
        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRecurringPaymentRequest request, CancellationToken ct)
    {
        var result = await recurringPaymentService.CreateAsync(request.Name, request.EstimatedAmount, request.CurrencyCode, request.Frequency, ct);
        return this.ToActionResult(result);
    }
}
