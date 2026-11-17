using EDOC.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Controllers;

[ApiController]
[Route("api/emergency-contacts")]
public sealed class EmergencyContactsController(HealthcareDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string regionCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(regionCode) || regionCode.Length > 16) return BadRequest(new { message = "A valid region code is required." });
        return Ok(await database.EmergencyContacts.AsNoTracking()
            .Where(x => x.RegionCode == regionCode && x.IsVerified)
            .OrderBy(x => x.ServiceType).Select(x => new { x.Id, x.RegionCode, x.ServiceType, x.DisplayName, x.PhoneNumber, x.VerifiedAt })
            .ToListAsync(cancellationToken));
    }
}
