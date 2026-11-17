# User guide

The app opens to a dashboard with doctor discovery, appointments, care-service shortcuts, and emergency information. Select the language button to switch between English and Khmer. The selected language is saved on the device and restored on next launch. Khmer UI strings use the included Khmer font.

Create a patient account from the sign-in screen or sign in with an existing account using either its email address or phone number and password. Cambodian local phone numbers and international numbers are supported. The app stores access and refresh tokens with the platform secure-storage provider. Doctor discovery and appointment history display live API data; the app shows an offline message if the API cannot be reached. The appointment screen does not fabricate empty bookings as successful responses.

Open **Profile** to update your full name, phone number, birth date, gender, address, and emergency contact. Email is read-only in the current version. Enter an optional birth date as `YYYY-MM-DD`; the API validates required fields and stores the profile under the signed-in patient account.

Emergency contact details appear only when a verified regional directory entry exists. Call local emergency services directly if the directory is empty; E-DOC does not make up a number. The telehealth endpoint stays unavailable until a real video provider integration is configured.

Doctors use their verified Doctor workspace to publish future availability and manage assigned appointments. Verified doctors can add diagnoses, laboratory results, and prescriptions only to their own confirmed/completed appointments. Administrators and HospitalStaff sign in through the web portal; hospital staff appointment access is restricted to their assigned facility.

Open **Health tracker** from the patient dashboard to save blood pressure, glucose, weight, and height measurements, or schedule one-time, daily, or weekly medication reminders. Due reminders can be marked taken or missed, and scheduled reminders can be cancelled. The schedule is stored by E-DOC, but device alerts and push delivery are not enabled until a notification provider is configured. Patients can grant a caregiver medication-reminder access from **Profile**; reminder reads are audited and denied when consent is absent or revoked.
# Patient appointment flow

Sign in or create a patient account, open **Doctors**, and choose **Book** beside a doctor. Select an available time, optionally enter a visit reason, and submit the booking. The API checks the slot again when saving; if another patient has taken it, choose a different time. Open **Appointments** to view bookings. Pending and confirmed appointments can be cancelled after confirmation.

Times are displayed in the device's local time zone. The appointment is saved with the selected doctor's schedule on the server. Online consultation remains unavailable until a real telehealth provider is configured.
