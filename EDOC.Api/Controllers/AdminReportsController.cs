using System.Text;
using EDOC.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Controllers;

[ApiController, Authorize(Roles = "Administrator,SuperAdmin")]
[Route("api/admin/reports")]
public sealed class AdminReportsController(HealthcareDbContext database) : ControllerBase
{
    [HttpGet("hospitals.csv")]
    public async Task<IActionResult> Hospitals(CancellationToken cancellationToken)
    {
        var data = await database.Hospitals.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new { x.Name, x.FacilityType, x.Ownership, x.NssfStatus,
                Province = x.Province == null ? "" : x.Province.Name, Address = x.Address ?? "", Phone = x.Phone ?? "", x.IsActive })
            .ToListAsync(cancellationToken);
        var rows = data.Select(x => new[] { x.Name, x.FacilityType, x.Ownership, x.NssfStatus,
            x.Province, x.Address, x.Phone, x.IsActive ? "Active" : "Inactive" }).ToArray();
        return Csv("hospitals", ["Hospital", "Type", "Ownership", "NSSF status", "Province", "Address", "Phone", "Account status"], rows);
    }

    [HttpGet("doctors.csv")]
    public async Task<IActionResult> Doctors(CancellationToken cancellationToken)
    {
        var data = await database.Doctors.AsNoTracking().OrderBy(x => x.User!.FullName)
            .Select(x => new { Name = x.User!.FullName, Specialty = x.Specialty!.Name, Hospital = x.Hospital == null ? "" : x.Hospital.Name,
                Qualifications = x.Qualifications ?? "", x.YearsExperience, x.IsApproved, Active = x.User.IsActive })
            .ToListAsync(cancellationToken);
        var rows = data.Select(x => new[] { x.Name, x.Specialty, x.Hospital, x.Qualifications,
            x.YearsExperience.ToString(), x.IsApproved ? "Verified" : "Pending", x.Active ? "Active" : "Inactive" }).ToArray();
        return Csv("doctors", ["Doctor", "Specialty", "Hospital", "Qualifications", "Experience years", "Verification", "Account status"], rows);
    }

    [HttpGet("patients.csv")]
    public async Task<IActionResult> Patients(CancellationToken cancellationToken)
    {
        var data = await database.Patients.AsNoTracking().OrderBy(x => x.User!.FullName)
            .Select(x => new { x.Id, Name = x.User!.FullName, Phone = x.User.PhoneNumber ?? "", Active = x.User.IsActive, x.CreatedAt })
            .ToListAsync(cancellationToken);
        var rows = data.Select(x => new[] { x.Id.ToString(), x.Name, x.Phone,
            x.Active ? "Active" : "Suspended", x.CreatedAt.ToString("O") }).ToArray();
        return Csv("patients", ["Patient ID", "Name", "Phone", "Account status", "Registered UTC"], rows);
    }

    [HttpGet("appointments.csv")]
    public async Task<IActionResult> Appointments([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var endDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = from ?? endDate.AddMonths(-1);
        if (endDate < startDate || endDate.DayNumber - startDate.DayNumber > 366)
            return BadRequest(new { message = "Choose a date range of up to 367 days." });
        var start = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(endDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var data = await database.Appointments.AsNoTracking().Where(x => x.Schedule!.StartsAt >= start && x.Schedule.StartsAt < end)
            .OrderBy(x => x.Schedule!.StartsAt).Take(10001)
            .Select(x => new { x.Id, Patient = x.Patient!.User!.FullName, Doctor = x.Doctor!.User!.FullName,
                Specialty = x.Doctor.Specialty!.Name, Hospital = x.Doctor.Hospital == null ? "" : x.Doctor.Hospital.Name,
                StartsAt = x.Schedule!.StartsAt, x.Status, x.IsTelehealth })
            .ToListAsync(cancellationToken);
        var rows = data.Select(x => new[] { x.Id.ToString(), x.Patient, x.Doctor, x.Specialty, x.Hospital,
            x.StartsAt.ToString("O"), x.Status.ToString(), x.IsTelehealth ? "Telehealth" : "In person" }).ToArray();
        if (rows.Length > 10000) return StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = "The selected date range contains more than 10,000 appointments. Narrow the range." });
        return Csv("appointments", ["Appointment ID", "Patient", "Doctor", "Specialty", "Hospital", "Starts UTC", "Status", "Visit type"], rows);
    }

    private FileContentResult Csv(string name, string[] headers, IReadOnlyCollection<string[]> rows)
    {
        var builder = new StringBuilder();
        AddLine(headers);
        foreach (var row in rows) AddLine(row);
        return File(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString()), "text/csv; charset=utf-8", $"edoc-{name}-{DateTime.UtcNow:yyyyMMdd}.csv");

        void AddLine(IEnumerable<string> values) => builder.AppendLine(string.Join(',', values.Select(Escape)));
        static string Escape(string value)
        {
            if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
    }
}
