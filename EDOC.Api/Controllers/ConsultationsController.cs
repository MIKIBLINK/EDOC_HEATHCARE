using System.Security.Claims;
using EDOC.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Controllers;

[ApiController, Authorize]
[Route("api/appointments/{appointmentId:guid}/consultation")]
public sealed class ConsultationsController(HealthcareDbContext database, IConfiguration configuration) : ControllerBase
{
    [HttpPost("join")]
    public async Task<IActionResult> Join(Guid appointmentId, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        var appointment = await database.Appointments.Include(x => x.Patient).Include(x => x.Doctor)
            .SingleOrDefaultAsync(x => x.Id == appointmentId, cancellationToken);
        if (appointment is null) return NotFound();
        var isAssignedDoctor = appointment.Doctor?.UserId == userId && User.IsInRole("Doctor") && appointment.Doctor.IsApproved;
        var permitted = appointment.Patient?.UserId == userId || isAssignedDoctor || User.IsInRole("Administrator");
        if (!permitted) return Forbid();
        if (!appointment.IsTelehealth || appointment.Status is not (Models.AppointmentStatus.Confirmed or Models.AppointmentStatus.Pending))
            return Conflict(new { message = "This appointment is not scheduled for telehealth." });
        var provider = configuration["Telehealth:Provider"];
        if (string.IsNullOrWhiteSpace(provider))
            return Problem("No telehealth provider is configured. Contact the service administrator.", statusCode: StatusCodes.Status503ServiceUnavailable);
        return Problem("The configured telehealth adapter is not available in this deployment.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
