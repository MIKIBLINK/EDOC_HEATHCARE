# Database

The EF Core code-first context is `EDOC.Api/Data/HealthcareDbContext.cs`. It targets SQL Server database `EDOC_HealthcareDB`, and Identity stores only password hashes. Main entities include users/roles, patients, doctors, specialties, hospitals/services/staff assignments, schedules, appointments, medical records, prescriptions, notifications, verified emergency contacts, consultation sessions, and hashed/rotating refresh tokens.

## Create and apply migrations

```powershell
dotnet-ef migrations add <MigrationName> --project EDOC.Api\EDOC.Api.csproj --startup-project EDOC.Api\EDOC.Api.csproj --output-dir Migrations
dotnet-ef database update --project EDOC.Api\EDOC.Api.csproj --startup-project EDOC.Api\EDOC.Api.csproj
```

The initial migration is already checked in and applied to the local development database. For future schema changes, use a new descriptive migration name.

The EF model seeds application roles and non-personal demonstration specialties plus one clearly labeled demonstration hospital. It does not seed user accounts, patient medical records, real contact numbers, or demo credentials. Emergency contacts are absent until an authorized operator verifies the numbers and inserts them.

Appointments have a filtered unique SQL Server index on schedule ID for every status except `Cancelled`. This makes one active booking per slot a database constraint. Refresh tokens are stored as SHA-256 hashes, expire, rotate on refresh, and use SQL Server row-version concurrency checks.

Private medical documents are stored outside static web content under `EDOC.Api/App_Data/MedicalRecords` by default. Back up and protect this folder as sensitive health information. Configure a protected data volume and malware scanning in a production deployment. Files are limited to PDF/PNG/JPEG, size-limited, signature-checked, and only served through an authorization-checked API endpoint.

The API does not run migrations automatically at startup. Apply reviewed migrations as a deployment step. Never point development migrations or seed data at a production database without a backup and explicit change review.
