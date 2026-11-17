using System.Security.Claims;
using EDOC.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Controllers;

[ApiController, Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(HealthcareDbContext database) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.Notifications.AsNoTracking().Where(x => x.UserId == UserId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.Title, x.Message, x.ReadAt, x.CreatedAt }).ToListAsync(cancellationToken);
        return Ok(new { items, page, pageSize, total });
    }
}
