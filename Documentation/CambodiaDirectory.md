# Cambodia healthcare directory import

## Imported reference data

- Administrative divisions: 25 provinces, 210 districts, and 1,661 communes from the 2025 Cambodia Geographical List, published through the Ministry of Economy and Finance portal and attributed there to MLMUPC with Ministry of Interior approval.
- Facility records: 36 named public facilities (23 provincial hospitals, 12 national hospitals, and one referral hospital) transcribed from Ministry of Health reports. The provincial/referral list is from the H-EQIP 2018 report, published in 2019; the national hospital names come from a 2024 Ministry of Health list quoted by the National Institute of Statistics.
- Specialties: the 25 English specialties requested for E-DOC, with Khmer display names.
- Provenance: source URL, source publication date, and retrieval/verification fields are retained in the database. The facility import intentionally contains no inferred addresses, phone numbers, email, websites, services, hours, doctors, schedules, or coordinates.

## Verification limits

The public HFMIS launch notice says Cambodia has a searchable private-facility directory and map, but the notice does not link to the public registry. The facility-level registry could not be retrieved during this import. The NSSF sources available for review likewise did not provide a current facility-level partner list. Therefore all 36 imported facilities have `NssfStatus = PENDING_VERIFICATION` and `OperationalStatus = PENDING_VERIFICATION`; none are presented as confirmed current partners, non-participants, or currently operating. No private facilities or external doctor profiles were imported. Existing E-DOC doctor, patient, user, appointment, and medical-record rows are preserved.

The existing doctor directory continues to return only active, approved E-DOC doctor accounts. External doctor rows require reliable public identity, specialty, and hospital affiliation sources and are not fabricated or attached to synthetic accounts.

## Import and migration

The import command is idempotent and preserves unmatched existing records:

```powershell
dotnet tool restore
dotnet-ef database update --project EDOC.Api --startup-project EDOC.Api
dotnet run --project EDOC.Api -- --import-cambodia-directory
```

An additive EF migration creates geography, source, NSSF-verification, and hospital-specialty tables and adds nullable directory metadata to `Hospitals`. The database backup was created and verified before applying it. The source JSON files are embedded in the API assembly so the import command does not depend on the shell's working directory.

## API directory filters

`GET /api/hospitals` supports `search`, `provinceCode`, `nssfStatus`, `facilityType`, `ownership`, `operationalStatus`, `page`, and `pageSize`. Province and specialty lookups are available at `/api/hospitals/provinces` and `/api/hospitals/specialties`. `GET /api/doctors` remains restricted to active, approved accounts and supports `specialtyId` and `hospitalId` filters.
