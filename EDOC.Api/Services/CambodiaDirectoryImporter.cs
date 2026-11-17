using System.Reflection;
using System.Text.Json;
using EDOC.Api.Data;
using EDOC.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Services;

public sealed class CambodiaDirectoryImporter(HealthcareDbContext database)
{
    private const string GeographyResource = "EDOC.Api.Data.DirectoryImport.cambodia-geography-2025.json";
    private const string FacilitiesResource = "EDOC.Api.Data.DirectoryImport.cambodia-facilities-reviewed.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly (string Key, string Name, string NameKm)[] SpecialtySeed =
    [
        ("general-medicine", "General Medicine", "វេជ្ជសាស្ត្រទូទៅ"), ("cardiology", "Cardiology", "ជំងឺបេះដូង"),
        ("dermatology", "Dermatology", "ជំងឺសើស្បែក"), ("pediatrics", "Pediatrics", "កុមារវិទ្យា"),
        ("obstetrics-gynecology", "Obstetrics and Gynecology", "សម្ភព និងរោគស្ត្រី"), ("neurology", "Neurology", "ប្រសាទវិទ្យា"),
        ("neurosurgery", "Neurosurgery", "វះកាត់ប្រព័ន្ធប្រសាទ"), ("orthopedics", "Orthopedics", "ឆ្អឹង និងសន្លាក់"),
        ("ophthalmology", "Ophthalmology", "ចក្ខុវិទ្យា"), ("ent", "ENT", "ត្រចៀក ច្រមុះ និងបំពង់ក"),
        ("dentistry", "Dentistry", "ទន្តសាស្ត្រ"), ("oncology", "Oncology", "មហារីកវិទ្យា"),
        ("urology", "Urology", "ប្រព័ន្ធទឹកនោម"), ("gastroenterology", "Gastroenterology", "ក្រពះ និងពោះវៀន"),
        ("endocrinology", "Endocrinology", "ប្រព័ន្ធអ័រម៉ូន"), ("psychiatry", "Psychiatry", "វិកលចរិត"),
        ("emergency-medicine", "Emergency Medicine", "វេជ្ជសាស្ត្រសង្គ្រោះបន្ទាន់"), ("surgery", "Surgery", "វះកាត់"),
        ("internal-medicine", "Internal Medicine", "វេជ្ជសាស្ត្រផ្ទៃក្នុង"), ("infectious-diseases", "Infectious Diseases", "ជំងឺឆ្លង"),
        ("radiology", "Radiology", "កាំរស្មីវេជ្ជសាស្ត្រ"), ("anesthesiology", "Anesthesiology", "ថ្នាំសន្លប់"),
        ("rehabilitation", "Rehabilitation", "ស្តារនីតិសម្បទា"), ("nephrology", "Nephrology", "តម្រងនោមវិទ្យា"),
        ("pulmonology", "Pulmonology", "ជំងឺសួត")
    ];

    public async Task<ImportResult> ImportAsync(CancellationToken cancellationToken = default)
    {
        var geography = await ReadEmbeddedAsync<GeographyImport>(GeographyResource, cancellationToken);
        var facilities = await ReadEmbeddedAsync<FacilitiesImport>(FacilitiesResource, cancellationToken);
        Validate(geography, facilities);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        await UpsertSourcesAsync(facilities.Sources, cancellationToken);
        var provinceRows = await database.Provinces.ToDictionaryAsync(x => x.Code, cancellationToken);
        foreach (var item in geography.Provinces)
        {
            if (!provinceRows.TryGetValue(item.Code, out var row))
            {
                provinceRows[item.Code] = row = new Province { Code = item.Code };
                database.Provinces.Add(row);
            }
            row.Name = item.NameEn;
            row.NameKm = item.NameKm;
        }
        await database.SaveChangesAsync(cancellationToken);

        var districtRows = await database.Districts.ToDictionaryAsync(x => x.Code, cancellationToken);
        foreach (var item in geography.Districts)
        {
            if (!districtRows.TryGetValue(item.Code, out var row)) districtRows[item.Code] = row = new District { Code = item.Code };
            row.ProvinceCode = item.ProvinceCode; row.Name = item.NameEn; row.NameKm = item.NameKm;
            if (database.Entry(row).State == EntityState.Detached) database.Districts.Add(row);
        }
        await database.SaveChangesAsync(cancellationToken);

        var communeRows = await database.Communes.ToDictionaryAsync(x => x.Code, cancellationToken);
        foreach (var item in geography.Communes)
        {
            if (!communeRows.TryGetValue(item.Code, out var row)) communeRows[item.Code] = row = new Commune { Code = item.Code };
            row.DistrictCode = item.DistrictCode; row.Name = item.NameEn; row.NameKm = item.NameKm;
            if (database.Entry(row).State == EntityState.Detached) database.Communes.Add(row);
        }
        await database.SaveChangesAsync(cancellationToken);

        var specialtyRows = await database.Specialties.ToListAsync(cancellationToken);
        foreach (var item in SpecialtySeed)
        {
            var row = specialtyRows.FirstOrDefault(x => string.Equals(x.SourceKey, $"directory:{item.Key}", StringComparison.OrdinalIgnoreCase))
                ?? specialtyRows.FirstOrDefault(x => string.Equals(x.Name, item.Name, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                row = new Specialty { Name = item.Name, SourceKey = $"directory:{item.Key}" };
                database.Specialties.Add(row);
                specialtyRows.Add(row);
            }
            else if (row.SourceKey is null) row.SourceKey = $"directory:{item.Key}";
            if (string.IsNullOrWhiteSpace(row.NameKm)) row.NameKm = item.NameKm;
        }
        await database.SaveChangesAsync(cancellationToken);

        var hospitals = await database.Hospitals.ToListAsync(cancellationToken);
        var resolvedHospitals = new Dictionary<string, Hospital>(StringComparer.Ordinal);
        var addedHospitals = 0;
        foreach (var item in facilities.Facilities)
        {
            var row = hospitals.FirstOrDefault(x => x.SourceRecordKey == item.SourceKey);
            row ??= hospitals.FirstOrDefault(x => string.Equals(x.Name, item.Name, StringComparison.OrdinalIgnoreCase)
                && (x.ProvinceCode is null || x.ProvinceCode == item.ProvinceCode));
            if (row is null)
            {
                row = new Hospital { Name = item.Name };
                database.Hospitals.Add(row);
                hospitals.Add(row);
                addedHospitals++;
            }
            row.SourceRecordKey ??= item.SourceKey;
            row.DataSourceKey = item.SourceKey.Split(':', 2)[0];
            row.ProvinceCode ??= item.ProvinceCode;
            row.FacilityType = item.FacilityType;
            row.Ownership = item.Ownership;
            row.NssfStatus = "PENDING_VERIFICATION";
            row.OperationalStatus = "PENDING_VERIFICATION";
            row.SourceUrl = item.SourceUrl;
            row.SourcePublishedOn = DateOnly.Parse(item.SourcePublishedOn);
            row.IsActive = true;
            resolvedHospitals[item.SourceKey] = row;
        }
        await database.SaveChangesAsync(cancellationToken);

        var checks = await database.NssfVerificationRecords.ToListAsync(cancellationToken);
        foreach (var item in facilities.Facilities)
        {
            var hospital = resolvedHospitals[item.SourceKey];
            var check = checks.FirstOrDefault(x => x.HospitalId == hospital.Id && x.CheckedOn == new DateOnly(2026, 11, 12));
            if (check is null)
            {
                database.NssfVerificationRecords.Add(new NssfVerificationRecord
                {
                    HospitalId = hospital.Id,
                    Status = "PENDING_VERIFICATION",
                    SourceUrl = "https://www.nssf.gov.kh/",
                    DataSourceKey = "nssf-public-site",
                    CheckedOn = new DateOnly(2026, 11, 12),
                    Notes = "No current facility-level NSSF partner confirmation was available in the reviewed public sources."
                });
            }
            else if (check.DataSourceKey is null) check.DataSourceKey = "nssf-public-site";
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ImportResult(geography.Provinces.Count, geography.Districts.Count, geography.Communes.Count,
            facilities.Facilities.Count, addedHospitals, SpecialtySeed.Length, 0);
    }

    private async Task UpsertSourcesAsync(Dictionary<string, SourceImport> sources, CancellationToken cancellationToken)
    {
        var existing = await database.DataSources.ToDictionaryAsync(x => x.Key, cancellationToken);
        foreach (var (key, source) in sources)
        {
            if (!existing.TryGetValue(key, out var row)) database.DataSources.Add(row = new DataSource { Key = key });
            row.Name = source.Name;
            row.Url = source.Url;
            row.PublishedOn = DateOnly.Parse(source.PublishedOn);
            row.RetrievedOn = new DateOnly(2026, 11, 12);
        }
        if (!existing.TryGetValue("cambodia-geography-2025", out var geo))
            database.DataSources.Add(geo = new DataSource { Key = "cambodia-geography-2025" });
        geo.Name = "Cambodia Geographical List 2025 (MLMUPC, released after MoI approval)";
        geo.Url = "https://data.mef.gov.kh/datasets/pd_68e370856a965e00074a5e7b";
        geo.PublishedOn = new DateOnly(2025, 12, 31);
        geo.RetrievedOn = new DateOnly(2026, 11, 12);
        if (!existing.TryGetValue("nssf-public-site", out var nssf))
            database.DataSources.Add(nssf = new DataSource { Key = "nssf-public-site" });
        nssf.Name = "National Social Security Fund public website (facility-level status not confirmed)";
        nssf.Url = "https://www.nssf.gov.kh/";
        nssf.PublishedOn = null;
        nssf.RetrievedOn = new DateOnly(2026, 11, 12);
        await database.SaveChangesAsync(cancellationToken);
    }

    private static async Task<T> ReadEmbeddedAsync<T>(string name, CancellationToken cancellationToken)
    {
        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded directory data resource '{name}' is missing.");
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException($"Directory data resource '{name}' is invalid.");
    }

    private static void Validate(GeographyImport geography, FacilitiesImport facilities)
    {
        if (geography.Provinces.Count != 25 || geography.Provinces.Select(x => x.Code).Distinct().Count() != 25)
            throw new InvalidDataException("Directory import requires 25 unique province codes.");
        var provinceCodes = geography.Provinces.Select(x => x.Code).ToHashSet(StringComparer.Ordinal);
        var districtCodes = geography.Districts.Select(x => x.Code).ToHashSet(StringComparer.Ordinal);
        if (geography.Districts.Any(x => !provinceCodes.Contains(x.ProvinceCode))
            || geography.Communes.Any(x => !districtCodes.Contains(x.DistrictCode)))
            throw new InvalidDataException("Geography import contains an invalid parent reference.");
        if (facilities.Facilities.Any(x => !provinceCodes.Contains(x.ProvinceCode) || string.IsNullOrWhiteSpace(x.Name)
            || string.IsNullOrWhiteSpace(x.SourceUrl) || string.IsNullOrWhiteSpace(x.SourceKey)))
            throw new InvalidDataException("Facility import contains incomplete source data or an invalid province code.");
        if (facilities.Facilities.Select(x => x.SourceKey).Distinct(StringComparer.Ordinal).Count() != facilities.Facilities.Count)
            throw new InvalidDataException("Facility import contains duplicate source keys.");
    }

    private sealed record GeographyImport(List<ProvinceImport> Provinces, List<DistrictImport> Districts, List<CommuneImport> Communes);
    private sealed record ProvinceImport(string Code, string NameEn, string NameKm);
    private sealed record DistrictImport(string Code, string ProvinceCode, string NameEn, string NameKm);
    private sealed record CommuneImport(string Code, string DistrictCode, string NameEn, string NameKm);
    private sealed record FacilitiesImport(Dictionary<string, SourceImport> Sources, List<FacilityImport> Facilities);
    private sealed record SourceImport(string Name, string Url, string PublishedOn);
    private sealed record FacilityImport(string Name, string ProvinceCode, string FacilityType, string Ownership, string SourceKey, string SourceUrl, string SourcePublishedOn);
}

public sealed record ImportResult(int Provinces, int Districts, int Communes, int FacilitySourceRows, int HospitalsInserted, int Specialties, int Doctors);
