using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDOC.Api.Migrations
{
    /// <inheritdoc />
    public partial class CambodiaHealthcareDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                table: "Specialties",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommuneCode",
                table: "Hospitals",
                type: "nvarchar(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistrictCode",
                table: "Hospitals",
                type: "nvarchar(4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacilityType",
                table: "Hospitals",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Hospitals",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Hospitals",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Hospitals",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameKm",
                table: "Hospitals",
                type: "nvarchar(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NssfStatus",
                table: "Hospitals",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "PENDING_VERIFICATION");

            migrationBuilder.AddColumn<string>(
                name: "ProvinceCode",
                table: "Hospitals",
                type: "nvarchar(2)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SourcePublishedOn",
                table: "Hospitals",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceRecordKey",
                table: "Hospitals",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "Hospitals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "VerifiedOn",
                table: "Hospitals",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Hospitals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DataSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PublishedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    RetrievedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HospitalSpecialties",
                columns: table => new
                {
                    HospitalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecialtyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HospitalSpecialties", x => new { x.HospitalId, x.SpecialtyId });
                    table.ForeignKey(
                        name: "FK_HospitalSpecialties_Hospitals_HospitalId",
                        column: x => x.HospitalId,
                        principalTable: "Hospitals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HospitalSpecialties_Specialties_SpecialtyId",
                        column: x => x.SpecialtyId,
                        principalTable: "Specialties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NssfVerificationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HospitalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CheckedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NssfVerificationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NssfVerificationRecords_Hospitals_HospitalId",
                        column: x => x.HospitalId,
                        principalTable: "Hospitals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Provinces",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameKm = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provinces", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Districts",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    ProvinceCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NameKm = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Districts", x => x.Code);
                    table.ForeignKey(
                        name: "FK_Districts_Provinces_ProvinceCode",
                        column: x => x.ProvinceCode,
                        principalTable: "Provinces",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Communes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    DistrictCode = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NameKm = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Communes", x => x.Code);
                    table.ForeignKey(
                        name: "FK_Communes_Districts_DistrictCode",
                        column: x => x.DistrictCode,
                        principalTable: "Districts",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Specialties_SourceKey",
                table: "Specialties",
                column: "SourceKey",
                unique: true,
                filter: "[SourceKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Hospitals_CommuneCode",
                table: "Hospitals",
                column: "CommuneCode");

            migrationBuilder.CreateIndex(
                name: "IX_Hospitals_DistrictCode",
                table: "Hospitals",
                column: "DistrictCode");

            migrationBuilder.CreateIndex(
                name: "IX_Hospitals_ProvinceCode_NssfStatus_FacilityType_IsActive",
                table: "Hospitals",
                columns: new[] { "ProvinceCode", "NssfStatus", "FacilityType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Hospitals_SourceRecordKey",
                table: "Hospitals",
                column: "SourceRecordKey",
                unique: true,
                filter: "[SourceRecordKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Communes_DistrictCode",
                table: "Communes",
                column: "DistrictCode");

            migrationBuilder.CreateIndex(
                name: "IX_DataSources_Key",
                table: "DataSources",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Districts_ProvinceCode",
                table: "Districts",
                column: "ProvinceCode");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalSpecialties_SpecialtyId",
                table: "HospitalSpecialties",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "IX_NssfVerificationRecords_HospitalId_CheckedOn",
                table: "NssfVerificationRecords",
                columns: new[] { "HospitalId", "CheckedOn" });

            migrationBuilder.AddForeignKey(
                name: "FK_Hospitals_Communes_CommuneCode",
                table: "Hospitals",
                column: "CommuneCode",
                principalTable: "Communes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Hospitals_Districts_DistrictCode",
                table: "Hospitals",
                column: "DistrictCode",
                principalTable: "Districts",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Hospitals_Provinces_ProvinceCode",
                table: "Hospitals",
                column: "ProvinceCode",
                principalTable: "Provinces",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hospitals_Communes_CommuneCode",
                table: "Hospitals");

            migrationBuilder.DropForeignKey(
                name: "FK_Hospitals_Districts_DistrictCode",
                table: "Hospitals");

            migrationBuilder.DropForeignKey(
                name: "FK_Hospitals_Provinces_ProvinceCode",
                table: "Hospitals");

            migrationBuilder.DropTable(
                name: "Communes");

            migrationBuilder.DropTable(
                name: "DataSources");

            migrationBuilder.DropTable(
                name: "HospitalSpecialties");

            migrationBuilder.DropTable(
                name: "NssfVerificationRecords");

            migrationBuilder.DropTable(
                name: "Districts");

            migrationBuilder.DropTable(
                name: "Provinces");

            migrationBuilder.DropIndex(
                name: "IX_Specialties_SourceKey",
                table: "Specialties");

            migrationBuilder.DropIndex(
                name: "IX_Hospitals_CommuneCode",
                table: "Hospitals");

            migrationBuilder.DropIndex(
                name: "IX_Hospitals_DistrictCode",
                table: "Hospitals");

            migrationBuilder.DropIndex(
                name: "IX_Hospitals_ProvinceCode_NssfStatus_FacilityType_IsActive",
                table: "Hospitals");

            migrationBuilder.DropIndex(
                name: "IX_Hospitals_SourceRecordKey",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "Specialties");

            migrationBuilder.DropColumn(
                name: "CommuneCode",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "DistrictCode",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "FacilityType",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "NameKm",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "NssfStatus",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "ProvinceCode",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "SourcePublishedOn",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "SourceRecordKey",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "VerifiedOn",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Hospitals");
        }
    }
}
