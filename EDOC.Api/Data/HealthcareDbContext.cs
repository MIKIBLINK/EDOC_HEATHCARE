using EDOC.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Data;

public sealed class HealthcareDbContext(DbContextOptions<HealthcareDbContext> options)
    : IdentityDbContext<UserAccount, UserRole, Guid>(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<CaregiverLink> CaregiverLinks => Set<CaregiverLink>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<Hospital> Hospitals => Set<Hospital>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Commune> Communes => Set<Commune>();
    public DbSet<HospitalSpecialty> HospitalSpecialties => Set<HospitalSpecialty>();
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<NssfVerificationRecord> NssfVerificationRecords => Set<NssfVerificationRecord>();
    public DbSet<HospitalService> HospitalServices => Set<HospitalService>();
    public DbSet<HospitalStaffAssignment> HospitalStaffAssignments => Set<HospitalStaffAssignment>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<Diagnosis> Diagnoses => Set<Diagnosis>();
    public DbSet<LabResult> LabResults => Set<LabResult>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<HealthMeasurement> HealthMeasurements => Set<HealthMeasurement>();
    public DbSet<MedicationReminder> MedicationReminders => Set<MedicationReminder>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<ConsultationSession> ConsultationSessions => Set<ConsultationSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AccountSecurityToken> AccountSecurityTokens => Set<AccountSecurityToken>();
    public DbSet<AccountSecurityLog> AccountSecurityLogs => Set<AccountSecurityLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<UserAccount>().Property(x => x.FullName).HasMaxLength(160).IsRequired();
        builder.Entity<UserAccount>().Property(x => x.PhoneNumber).HasMaxLength(32);
        builder.Entity<UserAccount>().HasIndex(x => x.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
        builder.Entity<UserAccount>().HasIndex(x => x.PhoneNumber).IsUnique().HasFilter("[PhoneNumber] IS NOT NULL");
        builder.Entity<Patient>().HasIndex(x => x.UserId).IsUnique();
        builder.Entity<Patient>().HasOne(x => x.User).WithOne().HasForeignKey<Patient>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CaregiverLink>().HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CaregiverLink>().HasOne(x => x.CaregiverUser).WithMany().HasForeignKey(x => x.CaregiverUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CaregiverLink>().Property(x => x.InviteEmail).HasMaxLength(256).IsRequired();
        builder.Entity<CaregiverLink>().Property(x => x.NormalizedInviteEmail).HasMaxLength(256).IsRequired();
        builder.Entity<CaregiverLink>().Property(x => x.InvitationTokenHash).HasMaxLength(64).IsRequired();
        builder.Entity<CaregiverLink>().HasIndex(x => x.InvitationTokenHash).IsUnique();
        builder.Entity<CaregiverLink>().HasIndex(x => new { x.PatientId, x.NormalizedInviteEmail }).IsUnique().HasFilter("[RevokedAt] IS NULL");
        builder.Entity<CaregiverLink>().HasIndex(x => new { x.CaregiverUserId, x.RevokedAt, x.AcceptedAt });
        builder.Entity<Doctor>().HasIndex(x => x.UserId).IsUnique();
        builder.Entity<Doctor>().Property(x => x.IsApproved).HasDefaultValue(false);
        builder.Entity<Doctor>().HasOne(x => x.User).WithOne().HasForeignKey<Doctor>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Specialty>().HasIndex(x => x.Name).IsUnique();
        builder.Entity<Specialty>().Property(x => x.SourceKey).HasMaxLength(80);
        builder.Entity<Specialty>().HasIndex(x => x.SourceKey).IsUnique().HasFilter("[SourceKey] IS NOT NULL");
        builder.Entity<Hospital>().HasIndex(x => x.Name);
        builder.Entity<Hospital>().Property(x => x.NameKm).HasMaxLength(240);
        builder.Entity<Hospital>().Property(x => x.FacilityType).HasMaxLength(40).IsRequired();
        builder.Entity<Hospital>().Property(x => x.Ownership).HasMaxLength(20).IsRequired().HasDefaultValue("Unknown");
        builder.Entity<Hospital>().Property(x => x.NssfStatus).HasMaxLength(32).IsRequired();
        builder.Entity<Hospital>().Property(x => x.OperationalStatus).HasMaxLength(32).IsRequired().HasDefaultValue("PENDING_VERIFICATION");
        builder.Entity<Hospital>().Property(x => x.FacilityType).HasDefaultValue("Unknown");
        builder.Entity<Hospital>().Property(x => x.NssfStatus).HasDefaultValue("PENDING_VERIFICATION");
        builder.Entity<Hospital>().Property(x => x.IsActive).HasDefaultValue(true);
        builder.Entity<Hospital>().Property(x => x.SourceRecordKey).HasMaxLength(200);
        builder.Entity<Hospital>().HasIndex(x => x.SourceRecordKey).IsUnique().HasFilter("[SourceRecordKey] IS NOT NULL");
        builder.Entity<Hospital>().HasIndex(x => new { x.ProvinceCode, x.NssfStatus, x.FacilityType, x.Ownership, x.IsActive });
        builder.Entity<Hospital>().HasOne(x => x.Province).WithMany().HasForeignKey(x => x.ProvinceCode).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Hospital>().HasOne(x => x.District).WithMany().HasForeignKey(x => x.DistrictCode).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Hospital>().HasOne(x => x.Commune).WithMany().HasForeignKey(x => x.CommuneCode).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Hospital>().Property(x => x.DataSourceKey).HasMaxLength(100);
        builder.Entity<Hospital>().HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceKey).HasPrincipalKey(x => x.Key).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Province>().HasKey(x => x.Code);
        builder.Entity<Province>().Property(x => x.Code).HasMaxLength(2);
        builder.Entity<Province>().Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Entity<Province>().Property(x => x.NameKm).HasMaxLength(120).IsRequired();
        builder.Entity<District>().HasKey(x => x.Code);
        builder.Entity<District>().Property(x => x.Code).HasMaxLength(4);
        builder.Entity<District>().Property(x => x.ProvinceCode).HasMaxLength(2).IsRequired();
        builder.Entity<District>().Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Entity<District>().Property(x => x.NameKm).HasMaxLength(160).IsRequired();
        builder.Entity<District>().HasOne(x => x.Province).WithMany(x => x.Districts).HasForeignKey(x => x.ProvinceCode).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Commune>().HasKey(x => x.Code);
        builder.Entity<Commune>().Property(x => x.Code).HasMaxLength(6);
        builder.Entity<Commune>().Property(x => x.DistrictCode).HasMaxLength(4).IsRequired();
        builder.Entity<Commune>().Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Entity<Commune>().Property(x => x.NameKm).HasMaxLength(160).IsRequired();
        builder.Entity<Commune>().HasOne(x => x.District).WithMany(x => x.Communes).HasForeignKey(x => x.DistrictCode).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<HospitalSpecialty>().HasKey(x => new { x.HospitalId, x.SpecialtyId });
        builder.Entity<HospitalSpecialty>().HasOne(x => x.Hospital).WithMany(x => x.Specialties).HasForeignKey(x => x.HospitalId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<HospitalSpecialty>().HasOne(x => x.Specialty).WithMany().HasForeignKey(x => x.SpecialtyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<HospitalSpecialty>().Property(x => x.SourceUrl).HasMaxLength(1000);
        builder.Entity<HospitalSpecialty>().Property(x => x.DataSourceKey).HasMaxLength(100);
        builder.Entity<HospitalSpecialty>().HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceKey).HasPrincipalKey(x => x.Key).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DataSource>().Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.Entity<DataSource>().HasAlternateKey(x => x.Key);
        builder.Entity<DataSource>().Property(x => x.Name).HasMaxLength(240).IsRequired();
        builder.Entity<DataSource>().Property(x => x.Url).HasMaxLength(1000).IsRequired();
        builder.Entity<NssfVerificationRecord>().Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Entity<NssfVerificationRecord>().Property(x => x.SourceUrl).HasMaxLength(1000).IsRequired();
        builder.Entity<NssfVerificationRecord>().HasOne(x => x.Hospital).WithMany().HasForeignKey(x => x.HospitalId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<NssfVerificationRecord>().Property(x => x.DataSourceKey).HasMaxLength(100);
        builder.Entity<NssfVerificationRecord>().HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceKey).HasPrincipalKey(x => x.Key).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<NssfVerificationRecord>().HasIndex(x => new { x.HospitalId, x.CheckedOn });
        builder.Entity<HospitalService>().Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Entity<HospitalStaffAssignment>().HasIndex(x => new { x.UserId, x.HospitalId }).IsUnique();
        builder.Entity<Doctor>().Property(x => x.ConsultationFee).HasPrecision(10, 2);
        builder.Entity<DoctorSchedule>().HasIndex(x => new { x.DoctorId, x.StartsAt, x.EndsAt });
        builder.Entity<Appointment>().HasIndex(x => new { x.ScheduleId, x.Status });
        builder.Entity<Appointment>().HasIndex(x => x.ScheduleId).IsUnique().HasFilter("[Status] <> 3");
        builder.Entity<Appointment>().HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Appointment>().HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Appointment>().HasOne(x => x.Schedule).WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<MedicalRecord>().HasIndex(x => new { x.PatientId, x.RecordedAt });
        builder.Entity<MedicalRecord>().HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.NoAction);
        builder.Entity<MedicalRecord>().HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<MedicalRecord>().HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Diagnosis>().Property(x => x.Code).HasMaxLength(32);
        builder.Entity<Diagnosis>().Property(x => x.CodeSystem).HasMaxLength(80);
        builder.Entity<Diagnosis>().Property(x => x.Display).HasMaxLength(240).IsRequired();
        builder.Entity<Diagnosis>().Property(x => x.ClinicalStatus).HasMaxLength(20).IsRequired();
        builder.Entity<Diagnosis>().Property(x => x.Notes).HasMaxLength(2000);
        builder.Entity<Diagnosis>().HasIndex(x => new { x.MedicalRecordId, x.CreatedAt });
        builder.Entity<Diagnosis>().HasOne(x => x.MedicalRecord).WithMany(x => x.Diagnoses).HasForeignKey(x => x.MedicalRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<LabResult>().Property(x => x.TestName).HasMaxLength(200).IsRequired();
        builder.Entity<LabResult>().Property(x => x.ResultValue).HasMaxLength(300);
        builder.Entity<LabResult>().Property(x => x.Unit).HasMaxLength(40);
        builder.Entity<LabResult>().Property(x => x.ReferenceRange).HasMaxLength(100);
        builder.Entity<LabResult>().Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Entity<LabResult>().Property(x => x.Notes).HasMaxLength(1000);
        builder.Entity<LabResult>().HasIndex(x => new { x.MedicalRecordId, x.CollectedAt });
        builder.Entity<LabResult>().HasOne(x => x.MedicalRecord).WithMany(x => x.LabResults).HasForeignKey(x => x.MedicalRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Prescription>().HasOne(x => x.MedicalRecord).WithMany(x => x.Prescriptions).HasForeignKey(x => x.MedicalRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<PrescriptionItem>().Property(x => x.MedicationName).HasMaxLength(200).IsRequired();
        builder.Entity<PrescriptionItem>().Property(x => x.Strength).HasMaxLength(80);
        builder.Entity<PrescriptionItem>().Property(x => x.Dose).HasMaxLength(120).IsRequired();
        builder.Entity<PrescriptionItem>().Property(x => x.Route).HasMaxLength(60).IsRequired();
        builder.Entity<PrescriptionItem>().Property(x => x.Frequency).HasMaxLength(120).IsRequired();
        builder.Entity<PrescriptionItem>().Property(x => x.Instructions).HasMaxLength(1000);
        builder.Entity<PrescriptionItem>().HasIndex(x => new { x.PrescriptionId, x.CreatedAt });
        builder.Entity<PrescriptionItem>().HasOne(x => x.Prescription).WithMany(x => x.Items).HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<HealthMeasurement>().Property(x => x.MetricType).HasMaxLength(24).IsRequired();
        builder.Entity<HealthMeasurement>().Property(x => x.Value).HasPrecision(10, 2);
        builder.Entity<HealthMeasurement>().Property(x => x.SecondaryValue).HasPrecision(10, 2);
        builder.Entity<HealthMeasurement>().Property(x => x.Unit).HasMaxLength(24).IsRequired();
        builder.Entity<HealthMeasurement>().Property(x => x.Notes).HasMaxLength(1000);
        builder.Entity<HealthMeasurement>().HasIndex(x => new { x.PatientId, x.MeasuredAt });
        builder.Entity<HealthMeasurement>().HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<MedicationReminder>().Property(x => x.MedicationName).HasMaxLength(200).IsRequired();
        builder.Entity<MedicationReminder>().Property(x => x.Dosage).HasMaxLength(120).IsRequired();
        builder.Entity<MedicationReminder>().Property(x => x.Instructions).HasMaxLength(1000);
        builder.Entity<MedicationReminder>().Property(x => x.Status).HasMaxLength(16).IsRequired();
        builder.Entity<MedicationReminder>().Property(x => x.LastDoseStatus).HasMaxLength(16);
        builder.Entity<MedicationReminder>().Property(x => x.RowVersion).IsRowVersion();
        builder.Entity<MedicationReminder>().HasIndex(x => new { x.PatientId, x.ScheduledAt, x.Status });
        builder.Entity<MedicationReminder>().HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Notification>().HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.Entity<EmergencyContact>().HasIndex(x => new { x.RegionCode, x.ServiceType });
        builder.Entity<EmergencyContact>().HasIndex(x => new { x.RegionCode, x.ServiceType, x.PhoneNumber }).IsUnique();
        builder.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
        builder.Entity<RefreshToken>().Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Entity<RefreshToken>().Property(x => x.RowVersion).IsRowVersion();
        builder.Entity<AccountSecurityToken>().Property(x => x.Purpose).HasConversion<int>();
        builder.Entity<AccountSecurityToken>().Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Entity<AccountSecurityToken>().Property(x => x.RowVersion).IsRowVersion();
        builder.Entity<AccountSecurityToken>().HasIndex(x => new { x.UserId, x.Purpose, x.ExpiresAt });
        builder.Entity<AccountSecurityToken>().HasIndex(x => x.TokenHash).IsUnique();
        builder.Entity<AccountSecurityLog>().Property(x => x.EventType).HasMaxLength(48).IsRequired();
        builder.Entity<AccountSecurityLog>().Property(x => x.ClientAddress).HasMaxLength(64);
        builder.Entity<AccountSecurityLog>().HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.Entity<AccountSecurityLog>().HasIndex(x => new { x.ResourceId, x.CreatedAt });
        builder.Entity<AccountSecurityLog>().HasIndex(x => new { x.EventType, x.CreatedAt });
        builder.Entity<AuditLog>().Property(x => x.Action).HasMaxLength(80).IsRequired();
        builder.Entity<AuditLog>().Property(x => x.EntityType).HasMaxLength(60).IsRequired();
        builder.Entity<AuditLog>().Property(x => x.Summary).HasMaxLength(500);
        builder.Entity<AuditLog>().HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AuditLog>().HasIndex(x => new { x.ActorUserId, x.CreatedAt });
        builder.Entity<AuditLog>().HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
        builder.Entity<RefreshToken>().HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.ReplacedByTokenId).OnDelete(DeleteBehavior.NoAction);
        builder.Entity<UserRole>().HasData(
            new UserRole { Id = Guid.Parse("e96f2bd0-170d-4c89-8bb5-e6d57086100a"), Name = "Patient", NormalizedName = "PATIENT", ConcurrencyStamp = "a1f03f2b-9e89-4a93-b6aa-24889ef22a01" },
            new UserRole { Id = Guid.Parse("e96f2bd0-170d-4c89-8bb5-e6d57086100b"), Name = "Doctor", NormalizedName = "DOCTOR", ConcurrencyStamp = "a1f03f2b-9e89-4a93-b6aa-24889ef22a02" },
            new UserRole { Id = Guid.Parse("e96f2bd0-170d-4c89-8bb5-e6d57086100c"), Name = "HospitalStaff", NormalizedName = "HOSPITALSTAFF", ConcurrencyStamp = "a1f03f2b-9e89-4a93-b6aa-24889ef22a03" },
            new UserRole { Id = Guid.Parse("e96f2bd0-170d-4c89-8bb5-e6d57086100d"), Name = "Administrator", NormalizedName = "ADMINISTRATOR", ConcurrencyStamp = "a1f03f2b-9e89-4a93-b6aa-24889ef22a04" },
            new UserRole { Id = Guid.Parse("e96f2bd0-170d-4c89-8bb5-e6d57086100e"), Name = "Caregiver", NormalizedName = "CAREGIVER", ConcurrencyStamp = "a1f03f2b-9e89-4a93-b6aa-24889ef22a05" },
            new UserRole { Id = Guid.Parse("e96f2bd0-170d-4c89-8bb5-e6d57086100f"), Name = "SuperAdmin", NormalizedName = "SUPERADMIN", ConcurrencyStamp = "a1f03f2b-9e89-4a93-b6aa-24889ef22a06" },
            new UserRole { Id = Guid.Parse("e96f2bd0-170d-4c89-8bb5-e6d570861010"), Name = "HospitalAdmin", NormalizedName = "HOSPITALADMIN", ConcurrencyStamp = "a1f03f2b-9e89-4a93-b6aa-24889ef22a07" });
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(IAuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                builder.Entity(entityType.ClrType).Property(nameof(IAuditableEntity.CreatedAt)).IsRequired();
                builder.Entity(entityType.ClrType).Property(nameof(IAuditableEntity.UpdatedAt)).IsRequired();
            }
        }
    }

    public override int SaveChanges()
    {
        StampAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampAuditFields()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = now;
            if (entry.State is EntityState.Added or EntityState.Modified) entry.Entity.UpdatedAt = now;
        }
    }
}
