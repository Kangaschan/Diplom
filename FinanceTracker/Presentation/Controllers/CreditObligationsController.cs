using Application.CreditObligations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

public sealed record CreateCreditObligationRequest(string Name, decimal RemainingAmount, decimal MonthlyPayment, decimal InterestRate, DateTime? EndDate);

[ApiController]
[Route("api/credit-obligations")]
[Authorize]
public sealed class CreditObligationsController(CreditObligationService creditObligationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await creditObligationService.GetListAsync(ct);
        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCreditObligationRequest request, CancellationToken ct)
    {
        var result = await creditObligationService.CreateAsync(request.Name, request.RemainingAmount, request.MonthlyPayment, request.InterestRate, request.EndDate, ct);
        return this.ToActionResult(result);
    }
}
