# E-DOC Healthcare

Cross-platform .NET MAUI client and ASP.NET Core API for a healthcare directory, patient accounts, appointments, and protected medical records.

## Projects

- `EDOC.Web` — secure, role-aware browser portal for Administrator and HospitalStaff accounts.
- `EDOC.Mobile` — .NET MAUI app for Android, iOS, and Windows, with MVVM, Shell tabs, English/Khmer resources, secure token storage, and an API client.
- `EDOC.Api` — ASP.NET Core REST API, JWT authentication, role authorization, EF Core SQL Server model, pagination, and private document storage.
- `EDOC.Api.Tests` — automated API model and security unit tests.
- `Database/Documentation` and `Documentation` — setup and operating guides.

## Current implementation boundary

Implemented API flows include patient registration/login/logout/refresh, doctor and hospital directory reads, doctor schedule creation and lookup, appointment booking/rescheduling/cancellation, patient profile reads/updates, authorized medical record reads/writes and private document storage, verified emergency directory reads, notifications, and an administrator statistics endpoint. The MAUI app includes dashboard, doctor directory with live slot selection and booking, appointment list with cancellation, sign in/register, editable patient profile and emergency contact, persisted English/Khmer choice, and secure token storage.

The mobile app serves Patient, Doctor, and Caregiver roles. Administrator and HospitalStaff roles use the separate `EDOC.Web` portal; its server-side cookie session keeps API bearer tokens out of the browser. The administrator workspace includes evidence-backed facility verification, specialty maintenance, the facility audit trail, doctor credential review, account activation, and staff assignment. HospitalStaff appointment access is scoped by existing hospital assignments.

Third-party telehealth, push delivery, outbound password reset email, maps, camera/photo profile upload, prescriptions editing, and production operations/monitoring require provider configuration and additional workflows. Telehealth requests return `503` until a real adapter is implemented and configured; the app does not fake a successful call. Emergency numbers are only returned after an administrator verifies and adds them. Demo seed data contains no patient accounts or records.

This is a development foundation and needs a security review, operational configuration, and clinical/workflow validation before handling real patient information.

## Quick start

See [Installation](Documentation/Installation.md), [Database](Documentation/Database.md), [API](Documentation/API.md), and [User guide](Documentation/UserGuide.md).
