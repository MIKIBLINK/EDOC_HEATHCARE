using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDOC.Api.Migrations
{
    /// <inheritdoc />
    public partial class CambodiaFacilityVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Hospitals_ProvinceCode_NssfStatus_FacilityType_IsActive",
                table: "Hospitals");

            migrationBuilder.AddColumn<string>(
                name: "OperationalStatus",
                table: "Hospitals",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "PENDING_VERIFICATION");

            migrationBuilder.AddColumn<string>(
                name: "Ownership",
                table: "Hospitals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.CreateIndex(
                name: "IX_Hospitals_ProvinceCode_NssfStatus_FacilityType_Ownership_IsActive",
                table: "Hospitals",
                columns: new[] { "ProvinceCode", "NssfStatus", "FacilityType", "Ownership", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Hospitals_ProvinceCode_NssfStatus_FacilityType_Ownership_IsActive",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "OperationalStatus",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "Ownership",
                table: "Hospitals");

            migrationBuilder.CreateIndex(
                name: "IX_Hospitals_ProvinceCode_NssfStatus_FacilityType_IsActive",
                table: "Hospitals",
                columns: new[] { "ProvinceCode", "NssfStatus", "FacilityType", "IsActive" });
        }
    }
}
