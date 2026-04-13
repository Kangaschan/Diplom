using Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

public sealed record ActivateSubscriptionRequest(int DurationDays);

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public sealed class SubscriptionsController(SubscriptionService subscriptionService) : ControllerBase
{
    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken ct)
    {
        var result = await subscriptionService.GetCurrentAsync(ct);
        return this.ToActionResult(result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(CancellationToken ct)
    {
        var result = await subscriptionService.GetHistoryAsync(ct);
        return this.ToActionResult(result);
    }

    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateSubscriptionRequest request, CancellationToken ct)
    {
        var result = await subscriptionService.ActivatePremiumAsync(request.DurationDays, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        var result = await subscriptionService.CancelPremiumAsync(ct);
        return this.ToActionResult(result);
    }
}
