using System.Security.Claims;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var result = await _notificationService.GetAllAsync(UserId, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var count = await _notificationService.GetUnreadCountAsync(UserId, ct);
        return Ok(new { count });
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        await _notificationService.MarkAsReadAsync(id, UserId, ct);
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        await _notificationService.MarkAllAsReadAsync(UserId, ct);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _notificationService.DeleteAsync(id, UserId, ct);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAll(CancellationToken ct)
    {
        await _notificationService.DeleteAllAsync(UserId, ct);
        return NoContent();
    }
}
