# Schema notes

The EF Core model defines SQL Server keys, foreign keys, filtered/unique constraints, audit timestamps, search indexes, and restrictive deletes for identity and appointment relationships. See `EDOC.Api/Models/HealthcareEntities.cs` and `EDOC.Api/Data/HealthcareDbContext.cs`. Generate the checked-in migration from those sources with the commands in `Documentation/Database.md`.

Sensitive tables: `AspNetUsers`, `Patients`, `Appointments`, `MedicalRecords`, `Prescriptions`, `Notifications`, `ConsultationSessions`, and `RefreshTokens`. Apply least-privilege SQL permissions, encryption at rest, protected backups, access auditing, and retention policy before storing real records.
