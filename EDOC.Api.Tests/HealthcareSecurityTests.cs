using EDOC.Api.Data;
using EDOC.Api.DTOs;
using EDOC.Api.Models;
using EDOC.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using EDOC.Api.Controllers;
using EDOC.Api.Services;

namespace EDOC.Api.Tests;

public sealed class HealthcareSecurityTests
{
    [Theory]
    [InlineData("+855 12 345 678", "+85512345678")]
    [InlineData("012 345 678", "+85512345678")]
    [InlineData("096123456", "+85596123456")]
    [InlineData("096 123 456", "+85596123456")]
    [InlineData("85512345678", "+85512345678")]
    public void Phone_login_identifiers_normalize_to_registered_e164_number(string identifier, string expected)
    {
        Assert.Equal(expected, LoginIdentifierParser.NormalizePhoneNumber(identifier));
    }

    [Fact]
    public void Email_is_not_treated_as_a_phone_login_identifier()
    {
        Assert.Null(LoginIdentifierParser.NormalizePhoneNumber("patient@example.test"));
        Assert.Null(LoginIdentifierParser.NormalizePhoneNumber("phone-unknown"));
    }

    [Fact]
    public void Administrator_management_controller_requires_administrator_role()
    {
        var authorize = typeof(AdminController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().Single();

        Assert.Contains("Administrator", authorize.Roles!.Split(','));
        Assert.Contains("SuperAdmin", authorize.Roles.Split(','));
    }

    [Fact]
    public void Administrator_directory_controller_requires_administrator_role()
    {
        var authorize = typeof(AdminDirectoryController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().Single();

        Assert.Contains("Administrator", authorize.Roles!.Split(','));
        Assert.Contains("SuperAdmin", authorize.Roles.Split(','));
    }

    [Fact]
    public void Hospital_admin_workspace_is_role_scoped_separately_from_global_admin()
    {
        var authorize = typeof(HospitalAdminController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().Single();

        Assert.Equal("HospitalAdmin", authorize.Roles);
    }

    [Fact]
    public void Doctor_workspace_profile_endpoint_requires_doctor_role()
    {
        var method = typeof(DoctorsController).GetMethod(nameof(DoctorsController.Mine))!;
        var authorize = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().Single();

        Assert.Equal("Doctor", authorize.Roles);
    }

    [Fact]
    public void Doctor_schedule_writes_and_clinical_record_writes_require_doctor_role()
    {
        var scheduleCreate = typeof(DoctorSchedulesController).GetMethod(nameof(DoctorSchedulesController.Create))!;
        var recordCreate = typeof(MedicalRecordsController).GetMethod(nameof(MedicalRecordsController.Create))!;

        Assert.Equal("Doctor", scheduleCreate.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().Single().Roles);
        Assert.Equal("Doctor", recordCreate.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().Single().Roles);
    }

    [Fact]
    public void Structured_clinical_endpoints_are_role_scoped()
    {
        var mine = typeof(ClinicalRecordsController).GetMethod(nameof(ClinicalRecordsController.Mine))!;
        var patientRecords = typeof(ClinicalRecordsController).GetMethod(nameof(ClinicalRecordsController.PatientRecords))!;
        var caregiverRecords = typeof(ClinicalRecordsController).GetMethod(nameof(ClinicalRecordsController.CaregiverRecords))!;
        var addDiagnosis = typeof(ClinicalRecordsController).GetMethod(nameof(ClinicalRecordsController.AddDiagnosis))!;

        Assert.Equal("Patient", mine.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
        Assert.Equal("Doctor,HospitalStaff", patientRecords.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
        Assert.Equal("Caregiver", caregiverRecords.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
        Assert.Equal("Doctor", addDiagnosis.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
    }

    [Fact]
    public void Health_measurement_and_reminder_endpoints_are_role_scoped()
    {
        var mine = typeof(HealthController).GetMethod(nameof(HealthController.MyMeasurements))!;
        var addMeasurement = typeof(HealthController).GetMethod(nameof(HealthController.AddMeasurement))!;
        var doctorMeasurements = typeof(HealthController).GetMethod(nameof(HealthController.PatientMeasurements))!;
        var caregiverReminders = typeof(HealthController).GetMethod(nameof(HealthController.CaregiverReminders))!;
        var addReminder = typeof(HealthController).GetMethod(nameof(HealthController.AddReminder))!;

        Assert.Equal("Patient", mine.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
        Assert.Equal("Patient", addMeasurement.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
        Assert.Equal("Doctor", doctorMeasurements.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
        Assert.Equal("Caregiver", caregiverReminders.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
        Assert.Equal("Patient", addReminder.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
    }

    [Fact]
    public void Medication_reminder_consent_is_an_explicit_request_flag()
    {
        var request = new UpdateCaregiverConsentRequest(false, false, true);
        Assert.True(request.CanViewMedicationReminders);
    }

    [Fact]
    public void IdentityPasswordHasher_does_not_store_the_original_password()
    {
        var user = new UserAccount { Id = Guid.NewGuid(), UserName = "patient@example.test", Email = "patient@example.test", FullName = "Example Patient" };
        var hasher = new PasswordHasher<UserAccount>();
        const string password = "StrongExample!123";
        var encoded = hasher.HashPassword(user, password);

        Assert.NotEqual(password, encoded);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, encoded, password));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, encoded, "wrong"));
    }

    [Fact]
    public void Appointment_model_has_database_constraint_for_one_active_booking_per_schedule()
    {
        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlServer("Server=(local);Database=EDOC_ModelTests;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var database = new HealthcareDbContext(options);
        var activeSlotIndex = database.Model.FindEntityType(typeof(Appointment))!.GetIndexes()
            .Single(index => index.IsUnique && index.Properties.Single().Name == nameof(Appointment.ScheduleId));

        Assert.Equal("[Status] <> 3", activeSlotIndex.GetFilter());
    }

    [Fact]
    public void Caregiver_consent_model_prevents_duplicate_live_invites_and_hashes_unique_tokens()
    {
        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlServer("Server=(local);Database=EDOC_ModelTests;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var database = new HealthcareDbContext(options);
        var entity = database.Model.FindEntityType(typeof(CaregiverLink))!;
        var liveInvite = entity.GetIndexes().Single(index => index.IsUnique && index.Properties.Select(p => p.Name)
            .SequenceEqual([nameof(CaregiverLink.PatientId), nameof(CaregiverLink.NormalizedInviteEmail)]));
        var tokenHash = entity.GetIndexes().Single(index => index.IsUnique && index.Properties.Select(p => p.Name)
            .SequenceEqual([nameof(CaregiverLink.InvitationTokenHash)]));

        Assert.Equal("[RevokedAt] IS NULL", liveInvite.GetFilter());
        Assert.True(tokenHash.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, entity.GetForeignKeys().Single(foreignKey => foreignKey.Properties.Single().Name == nameof(CaregiverLink.PatientId)).DeleteBehavior);
    }

    [Fact]
    public void Caregiver_account_type_passes_registration_validation()
    {
        var request = new RegisterRequest("Example Caregiver", "caregiver@example.test", "StrongExample!123",
            "+85512345678", "StrongExample!123", "Caregiver");
        var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request,
            new System.ComponentModel.DataAnnotations.ValidationContext(request), errors, validateAllProperties: true);

        Assert.True(valid, string.Join("; ", errors.Select(x => x.ErrorMessage)));
    }

    [Fact]
    public void Localizer_switches_between_English_and_Khmer_resources()
    {
        var localizer = new AppLocalizer();
        var english = localizer.Get("welcome");
        localizer.SetLanguage("km");
        var khmer = localizer.Get("welcome");

        Assert.Equal("Good health starts with the right care.", english);
        Assert.Contains("សុខភាព", khmer);
        Assert.NotEqual(english, khmer);
        Assert.Equal("km", localizer.Language);
    }

    [Fact]
    public void Phone_or_email_login_label_is_available_in_English_and_Khmer()
    {
        var localizer = new AppLocalizer();
        localizer.SetLanguage("en");
        var english = localizer.Get("login_identifier");
        localizer.SetLanguage("km");
        var khmer = localizer.Get("login_identifier");

        Assert.Equal("Phone number (e.g. 096123456) or email", english);
        Assert.Contains(khmer, character => character is >= '\u1780' and <= '\u17FF');
        Assert.NotEqual(english, khmer);
        localizer.SetLanguage("en");
        Assert.Equal("Remember me", localizer.Get("remember_me"));
    }

    [Fact]
    public void Administrator_directory_labels_are_available_in_English_and_Khmer()
    {
        var localizer = new AppLocalizer();
        var keys = new[] { "admin_directory_title", "admin_hospital_name", "admin_evidence_required", "admin_specialty_name" };

        foreach (var key in keys)
        {
            localizer.SetLanguage("en");
            var english = localizer.Get(key);
            localizer.SetLanguage("km");
            var khmer = localizer.Get(key);

            Assert.NotEqual(key, english);
            Assert.NotEqual(key, khmer);
            Assert.NotEqual(english, khmer);
            Assert.Contains(khmer, character => character is >= '\u1780' and <= '\u17FF');
        }
    }
}
