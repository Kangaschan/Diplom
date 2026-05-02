using Application.Auth;
using Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;
using Shared.Results;
using Subscriptions.Interfaces;
using Subscriptions.Models.Request;
using Subscriptions.Models.Response;

namespace Presentation.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly SubscriptionService _subscriptionService;
    private readonly IAuthService _authService;
    private readonly IStripeService _stripeService;
    private readonly IStripeWebhookService _stripeWebhookService;

    public SubscriptionsController(
        SubscriptionService subscriptionService,
        IAuthService authService,
        IStripeService stripeService,
        IStripeWebhookService stripeWebhookService)
    {
        _subscriptionService = subscriptionService;
        _authService = authService;
        _stripeService = stripeService;
        _stripeWebhookService = stripeWebhookService;
    }

    [AllowAnonymous]
    [HttpGet("plans")]
    public IActionResult Plans()
    {
        var result = _stripeService.GetPricesInfo();
        return this.ToActionResult(result);
    }

    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken ct)
    {
        var result = await _subscriptionService.GetCurrentAsync(ct);
        return this.ToActionResult(result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(CancellationToken ct)
    {
        var result = await _subscriptionService.GetHistoryAsync(ct);
        return this.ToActionResult(result);
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, CancellationToken ct)
    {
        var userResult = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return this.ToActionResult(Result<string>.Failure(userResult.Error));
        }

        var result = await _stripeService.CreateCheckoutSessionAsync(userResult.Value.Id, request, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("portal")]
    public async Task<IActionResult> Portal(CancellationToken ct)
    {
        var userResult = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return this.ToActionResult(Result<string>.Failure(userResult.Error));
        }

        var result = await _stripeService.CreateCustomerPortalSessionAsync(userResult.Value.Id, ct);
        return this.ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        var message = new StrpeWebHookMessage
        {
            Json = json,
            Headers = signature
        };

        var result = await _stripeWebhookService.HandleWebhookAsync(message, ct);
        return this.ToActionResult(result);
    }

}
