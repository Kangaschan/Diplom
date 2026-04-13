using Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(NotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool unreadOnly, CancellationToken ct)
    {
        var result = await notificationService.GetListAsync(unreadOnly, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        var result = await notificationService.GetUnreadCountAsync(ct);
        return this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var result = await notificationService.MarkReadAsync(id, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var result = await notificationService.MarkAllReadAsync(ct);
        return this.ToActionResult(result);
    }
}
