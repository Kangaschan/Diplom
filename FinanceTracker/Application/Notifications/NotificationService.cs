using Application.Abstractions;
using Application.Auth;
using Domain.Common;
using Domain.Notifications;
using Shared.Results;

namespace Application.Notifications;

public sealed class NotificationService(IFinanceDbContext dbContext, IAuthService authService)
{
    public async Task<Result<IReadOnlyCollection<Notification>>> GetListAsync(bool unreadOnly = false, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<IReadOnlyCollection<Notification>>.Failure(user.Error);

        var query = dbContext.Notifications.Where(n => n.UserId == user.Value.Id);
        if (unreadOnly) query = query.Where(n => !n.IsRead);

        return Result<IReadOnlyCollection<Notification>>.Success(query.OrderByDescending(n => n.CreatedAt).ToList());
    }

    public async Task<Result<int>> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<int>.Failure(user.Error);

        var count = dbContext.Notifications.Count(n => n.UserId == user.Value.Id && !n.IsRead);
        return Result<int>.Success(count);
    }

    public async Task<Result> MarkReadAsync(Guid id, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result.Failure(user.Error);

        var notification = dbContext.Notifications.FirstOrDefault(n => n.Id == id && n.UserId == user.Value.Id);
        if (notification is null) return Result.Failure(AppErrors.NotFound("Notification not found."));

        notification.IsRead = true;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> MarkAllReadAsync(CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result.Failure(user.Error);

        var notifications = dbContext.Notifications.Where(n => n.UserId == user.Value.Id && !n.IsRead).ToList();
        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task CreateSystemAsync(Guid userId, string title, string message, NotificationType type = NotificationType.System, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            CreatedAt = DateTime.UtcNow
        };

        await dbContext.AddAsync(notification, ct);
        await dbContext.SaveChangesAsync(ct);
    }
}
