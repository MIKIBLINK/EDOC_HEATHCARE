# API reference

Development OpenAPI JSON is available at `/openapi/v1.json` when the API runs in Development. `/health` is a liveness endpoint and does not indicate database readiness.

## Authentication

- `POST /api/auth/register` — create a patient account and return access/refresh tokens.
- `POST /api/auth/login` — authenticate with `{ "identifier": "email or phone", "password": "…" }` and issue tokens. Cambodian local phone numbers and international E.164 numbers are accepted; the older `{ "email": "…", "password": "…" }` body remains supported.
- `POST /api/auth/refresh` — rotate one refresh token; concurrent reuse is rejected.
- `POST /api/auth/logout` — revoke the caller's refresh token.

Passwords use ASP.NET Core Identity hashing and policy checks. Access tokens expire after 15 minutes; refresh tokens after 14 days. Configure `Jwt:Key` in user-secrets or environment variables. Forgot-password email is not implemented until an outbound email provider is configured.

## Directory and schedules

- `GET /api/doctors?search=&specialtyId=&page=1&pageSize=20`
- `GET /api/doctors/{id}`
- `GET /api/doctors/specialties`
- `GET /api/doctors/{id}/schedules?from=&to=` — future available slots only.
- `GET /api/doctors/me` — Doctor role; returns the caller's clinical workspace profile and verification state.
- `GET /api/doctors/me/schedules?from=&to=` — Doctor role; returns the caller's own schedule availability and reservations.
- `POST /api/doctors/me/schedules` — verified Doctor role; prevents overlapping schedules under a serializable transaction.
- `GET /api/hospitals?page=1&pageSize=20&search=`
- `GET /api/hospitals/{id}`
- `GET /api/emergency-contacts?regionCode=KH` — returns verified entries only.

## Patients and appointments

- `GET /api/patients/me`, `PUT /api/patients/me`
- `GET /api/patients/{id}` — doctor must have an appointment with the patient; staff must be assigned to the hospital associated with an appointment; administrator access is role-controlled.
- `GET /api/appointments?page=1&pageSize=20` — scoped to patient, assigned doctor, or assigned hospital staff.
- `POST /api/appointments` — Patient role; server validates that the doctor is verified and the slot is available; the database prevents duplicate active booking.
- `PUT /api/appointments/{id}` — patient-owned reschedule to a future free slot with the same doctor.
- `DELETE /api/appointments/{id}` — authorized cancellation.
- `POST /api/appointments/{id}/consultation/join` — returns `503` until a real telehealth adapter is configured.

## Records and notifications

- `GET /api/medical-records?page=1&pageSize=20&patientId=` - owner, assigned doctor, assigned hospital staff, or administrator scopes.
- `POST /api/medical-records` - Doctor role, requires an appointment connecting that doctor and patient.
- `POST /api/medical-records/{id}/documents`, `GET /api/medical-records/{id}/documents` - private, authorized file storage/retrieval.
- `GET /api/clinical-records/me?page=1&pageSize=30` - Patient role; returns the caller's diagnoses, lab results, and prescriptions.
- `GET /api/clinical-records/patients/{patientId}` - verified Doctor with a confirmed/completed appointment, or hospital staff assigned to the associated hospital.
- `GET /api/clinical-records/caregiver-links/{linkId}` - Caregiver role; requires an accepted, active link with medical-record consent.
- `POST /api/clinical-records/appointments/{appointmentId}/diagnoses`, `/lab-results`, or `/prescriptions` - verified Doctor only, scoped to the doctor's own confirmed/completed appointment.
- `GET /api/health/measurements/me` and `POST /api/health/measurements` - Patient's private measurement history; verified Doctors can read a patient's history only with a confirmed/completed appointment.
- `GET /api/health/reminders/me`, `POST /api/health/reminders`, and `PUT /api/health/reminders/{id}/status` - Patient's medication schedule and taken/missed actions. Repeating schedules advance after an action and use row-version concurrency checks.
- `GET /api/health/caregiver-links/{linkId}/reminders` - Caregiver read access only when the patient explicitly grants medication-reminder consent.
- `GET /api/notifications?page=1&pageSize=30` - current user only.
- `GET /api/admin/dashboard` — global `Administrator` or `SuperAdmin`; live hospital/NSSF, doctor verification, patient, staff, appointment and registration counts.
- `GET /api/admin/patients?search=&active=&page=1&pageSize=30` — paged identity/account directory; does not return clinical records.
- `GET /api/admin/staff?search=&hospitalId=&page=1&pageSize=30` — hospital staff assignment directory.
- `GET /api/admin/doctors?search=&approved=false&page=1&pageSize=30` — global administrator; paged verification queue.
- `POST /api/admin/doctors` and `PUT /api/admin/doctors/{doctorId}/approval` — creates an unapproved profile and lets global admins review it; rejection suspends the account and revokes refresh tokens.
- `GET /api/admin/users?search=&page=1&pageSize=30` and `PUT /api/admin/users/{userId}/activation` — account directory and activation controls.
- `POST /api/admin/hospitals/{hospitalId}/admins/{userId}` — assign a hospital admin role and facility membership; global-admin only.
- `GET /api/hospital-admin/hospitals`, `GET /api/hospital-admin/hospitals/{hospitalId}/dashboard`, `GET /api/hospital-admin/hospitals/{hospitalId}/doctors`, `GET /api/hospital-admin/hospitals/{hospitalId}/appointments`, and `PUT /api/hospital-admin/hospitals/{hospitalId}/appointments/{appointmentId}/status` — facility-scoped hospital admin workspace. Every request checks the caller's assignment.
- `GET|POST /api/hospital-admin/hospitals/{hospitalId}/doctors/{doctorId}/schedules` and `PUT /api/hospital-admin/hospitals/{hospitalId}/doctors/{doctorId}/schedules/{scheduleId}/availability` — facility-scoped schedule listing and management for active, verified doctors; overlapping slots and reopening booked slots are rejected.
- `GET /api/appointments?search=&status=&hospitalId=&doctorId=&date=&page=1&pageSize=20` — role-scoped booking directory; filters narrow the user's existing access scope.
- `GET /api/admin/reports/{hospitals|doctors|patients|appointments}.csv` — global-admin CSV exports. Appointment exports accept optional `from`/`to` dates and cap results at 10,000 rows.

All list endpoints cap page size. Clinical record reads create access audit events. Controllers use DTO projections and do not return password hashes, refresh token hashes, or document storage keys.
