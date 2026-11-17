# E-DOC Development Plan

## Environment and safety

- Working tree: `D:\my project\EDOC`.
- .NET SDK: `10.0.401`; Android, iOS, Mac Catalyst, and MAUI Windows workloads are installed.
- SQL Server Express service `MSSQL$SQLEXPRESS` is running. The API uses integrated authentication to `localhost\SQLEXPRESS`, database `EDOC_HealthcareDB`.
- The API `/health` endpoint responds. `dotnet ef migrations list` connects successfully and reports the checked-in migrations as applied.
- Source backup: `D:\my project\EDOC.source-backup-20261111-234500`. Database backup before the Cambodia directory import: `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\EDOC_HealthcareDB_before_Cambodia_directory_20261112.bak`.
- This Windows machine has the iOS workload, but a signed iOS build still needs a paired Mac/Xcode. `adb` is not on `PATH`; Android targets build, but device deployment requires locating the installed Android SDK tools.

## Existing architecture

- `EDOC.Mobile`: .NET MAUI Shell, XAML pages, CommunityToolkit.Mvvm view models, DI, API/session/language services, shared styles, English/Khmer resources, and platform assets.
- `EDOC.Web`: ASP.NET Core Razor Pages portal with protected HttpOnly-cookie sign-in and server-side API token refresh for Administrator and HospitalStaff roles.
- `EDOC.Api`: ASP.NET Core 10, Identity Core, JWT, role authorization, SQL Server EF Core, migrations, private record files, rate limited auth, and controllers.
- `EDOC.Localization`: resource-backed English and Khmer localizer.
- `EDOC.Api.Tests`: API model/security tests.

## Existing working modules

- Patient and caregiver registration/login flows, email verification and password reset endpoints (email delivery requires configuration), JWT/refresh tokens, role authorization, and secure token storage.
- Patient profile editing, doctor/hospital directories, available doctor schedules, booking/rescheduling/cancellation APIs, patient appointment and record views, private medical document endpoints, and emergency contact directory.
- Caregiver invitation, explicit consent, revocation, and authorized shared appointment/record reads.
- Structured diagnoses, lab results, and prescription items; patient health measurements; one-time and recurring medication schedules; caregiver medication-reminder access controlled by patient consent.
- Administrator portal dashboard, doctor approval, user activation, hospital staff assignment, and paginated facility directory; HospitalStaff portal appointments are limited by assigned hospital.
- English/Khmer resources and Khmer font registration; patient/caregiver Shell tabs; light/dark theme settings.

## Gaps found

- Doctor and Caregiver have mobile workspaces; Administrator and HospitalStaff use the web portal. Hospital/specialty management is available through a protected administrator API/workspace; expanded reporting remains future work.
- The API now contains structured diagnoses, lab results, prescription items, health measurements, and medication reminder schedules. Pharmacy listings, payments, and persisted AI conversations are not implemented.
- The AI page currently explains that no provider is configured; it does not fabricate an answer. Telehealth returns unavailable until a provider is configured. Notifications are stored/read through the API, but no push-delivery provider is configured.
- The mobile registration flow exposes Patient/Caregiver only. Doctor self-onboarding and credential upload/review details remain to be built; administrators can create doctor profiles through the API.
- Current doctor data has no rating/review/photo fields. Map, pharmacy, payments, and medication data providers are absent.
- Tests cover security metadata, credential hashing, data constraints, caregiver consent shape, and localization (10 API tests); there are no UI automation or database integration tests yet.

## Phased plan

1. **Inspection and backup — complete.** Inspect pages, view models, services, API, EF model, migrations, configuration, workloads, database connectivity, build, and tests. Preserve existing staged and uncommitted changes.
2. **Architecture — largely present.** Keep the existing projects and MVVM/DI boundaries. Add role-specific views/view models and API client operations without moving business logic into XAML.
3. **Database — in progress.** Clinical diagnoses/labs/prescriptions and patient health measurements/reminders now have additive SQL Server migrations, role-scoped API operations, audit events, and authorization tests. Never reset or auto-migrate the existing database.
4. **Authentication/permissions — existing foundation.** Preserve Identity/JWT, verification, lockout, rate limits, caregiver consent scopes, and doctor approval checks. Add tests for any changed policy.
5. **Shared UI/navigation — underway.** Continue the shared glass design, responsive layouts, dynamic theme, language switching, role-aware Shell, and accessible loading/empty/error states.
6. **Patient — partially implemented.** Existing profile, doctors, appointments, records, caregiver links, hospitals, and emergency directory. Improve booking/rescheduling UX and expose only backend-supported data.
7. **Doctor — workspace implemented; expand patient management.** Protected workspace includes profile verification status, assigned appointments, appointment state actions, and schedule creation. Doctors can add structured clinical records and read health measurements for confirmed/completed patients through the API.
8. **Caregiver — partially implemented.** Keep consent-gated reads and add only supported reminder/alert behavior after the backend exists.
9. **Administrator — web portal implemented.** Protected dashboard includes platform metrics, doctor approval/rejection, account activation, hospital staff assignment, and facility directory. Specialty and advanced reporting workflows remain.
10. **Appointments/records — partially implemented.** Booking, authorization, structured diagnoses/lab results/prescriptions, patient record access, and caregiver consent-gated records are in place. Authenticated end-to-end clinical record entry still needs a seeded test appointment in an isolated test database.
11. **AI — provider absent.** Add a server-side provider abstraction with safe refusal/fallback and conversation persistence; live responses require a server secret/provider configuration. Never place keys in MAUI.
12. **Notifications/health — in progress.** Measurements and recurring medication schedule records exist with patient and consent-scoped caregiver APIs. Device alerts/push delivery and notification read-state actions still need a real delivery layer and workflow verification.
13. **Directories/integrations — partial.** Hospitals and verified emergency contacts exist. Pharmacy/map content requires a configured data source; payments/video/push/device integrations must remain visibly unavailable until real providers are configured.
14. **Localization — existing foundation.** Keep English/Khmer keys in parity and validate Khmer rendering for every new screen.
15. **Quality — ongoing.** Build Android/Windows/iOS targets where supported; run API tests, XAML/resource checks, and database tests. Device/runtime and iOS signing checks require their respective tooling.

## Current milestone — role-separated clients and portal

- Mobile Shell provides Patient, Doctor, and Caregiver workspaces. Administrator and HospitalStaff accounts are routed to the web portal; they are not shown patient tabs.
- Added a Doctor workspace for the verified profile, assigned appointment actions, and schedule publishing, backed by the existing protected API.
- Added `EDOC.Web` with secure API-backed sign-in, role-protected Administrator and HospitalStaff pages, live admin metrics, doctor approval, account activation, hospital assignment, evidence-backed facility status editing, specialty maintenance, audit trail, and assigned appointment status actions.
- Enforced doctor verification before publishing availability, booking from a doctor slot, changing appointment status, joining telehealth as a doctor, or accessing/writing clinical records.
- Added English and Khmer labels for the new surfaces and kept resource keys in parity.
- Added Khmer/English messages for the mobile web-role notice. Localization resources are in parity (256 keys each).
- API tests pass (10/10). API and Web builds pass with zero warnings/errors; Android builds with zero warnings/errors; Windows builds with existing CommunityToolkit AOT analyzer warnings. Anonymous web-page access redirects to sign-in. Admin API smoke checks return the imported live records, reject unverified NSSF promotion, and reject anonymous access.
- Database was not reset or changed in this role-split milestone. Current preserved counts: 5 users, 1 patient, 1 doctor, 37 hospitals, 26 specialties, and 0 admin-directory audit entries.
- Production signing, deployment URLs, live role-account browser walkthrough, and device installation remain pending.

The role-separated clients and portal remain the foundation; see the current clinical and health milestone below for subsequent work.

## Current milestone — clinical records and health tracking

- Added additive `ClinicalRecordsV1`, `PatientHealthTracking`, and `RecurringMedicationReminders` SQL Server migrations. Each database update was preceded by a verified copy-only backup.
- Patients can save blood pressure, glucose, weight, and height measurements with validation; verified doctors can read measurements for patients with confirmed/completed appointments.
- Patients can schedule one-time, daily, or weekly medication reminders and mark doses taken or missed. Repeating reminders advance to their next occurrence and use SQL Server row-version concurrency protection.
- Patients explicitly control whether linked caregivers can read medication reminders. Consent is enforced in the API and access is audited.
- Mobile health tracking UI is available to patients and localized in English/Khmer. Reminder schedules are persisted, but device/push alerts are not delivered until a notification provider is configured.
- API build, Android build, and 13 API security tests pass. Windows still emits CommunityToolkit MVVM AOT analyzer warnings; no authenticated UI-to-database workflow test has been performed because the database contains no appointments or medication/measurement rows.

Next: create isolated database integration tests for health and clinical writes, then implement the missing AI conversation/persistence provider and notification delivery adapter without exposing secrets in MAUI.
