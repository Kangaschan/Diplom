using Application.Abstractions;
using Application.Auth;
using Application.Subscriptions;
using Domain.Common;
using Shared.Results;
using System.Text;

namespace Application.Exports;

public sealed class ExportService(IFinanceDbContext dbContext, IAuthService authService, IPremiumAccessService premiumAccessService)
{
    public async Task<Result<string>> ExportTransactionsCsvAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<string>.Failure(user.Error);

        var premium = premiumAccessService.EnsurePremium(user.Value);
        if (premium.IsFailure) return Result<string>.Failure(premium.Error);

        var rows = dbContext.Transactions
            .Where(t => t.UserId == user.Value.Id && t.TransactionDate >= from && t.TransactionDate <= to)
            .OrderByDescending(t => t.TransactionDate)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("id,date,type,amount,currency,description");
        foreach (var row in rows)
        {
            sb.AppendLine($"{row.Id},{row.TransactionDate:O},{row.Type},{row.Amount},{row.CurrencyCode},\"{row.Description?.Replace("\"", "'" )}\"");
        }

        return Result<string>.Success(sb.ToString());
    }
}
