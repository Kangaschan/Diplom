using Application.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

public sealed record CreateAccountRequest(string Name, string CurrencyCode, decimal InitialBalance);
public sealed record UpdateAccountRequest(string Name, bool IsArchived, decimal? FinancialGoalAmount, DateTime? FinancialGoalDeadline);
public sealed record SetBalanceRequest(decimal NewBalance);
public sealed record TransferRequest(Guid FromAccountId, Guid ToAccountId, decimal Amount, string CurrencyCode, string? Description);

[ApiController]
[Route("api/accounts")]
[Authorize]
public sealed class AccountsController(AccountService accountService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeArchived, CancellationToken ct)
    {
        var result = await accountService.GetListAsync(includeArchived, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await accountService.GetByIdAsync(id, ct);
        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken ct)
    {
        var result = await accountService.CreateAsync(request.Name, request.CurrencyCode, request.InitialBalance, ct);
        return this.ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountRequest request, CancellationToken ct)
    {
        var result = await accountService.UpdateAsync(id, request.Name, request.IsArchived, request.FinancialGoalAmount, request.FinancialGoalDeadline, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await accountService.ArchiveAsync(id, ct);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/balance")]
    public async Task<IActionResult> SetBalance(Guid id, [FromBody] SetBalanceRequest request, CancellationToken ct)
    {
        var result = await accountService.SetBalanceAsync(id, request.NewBalance, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request, CancellationToken ct)
    {
        var result = await accountService.TransferAsync(request.FromAccountId, request.ToAccountId, request.Amount, request.CurrencyCode, request.Description, ct);
        return this.ToActionResult(result);
    }
}
