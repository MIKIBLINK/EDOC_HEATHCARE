using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDOC.Api.Migrations
{
    /// <inheritdoc />
    public partial class VerifiedHospitalSpecialties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataSourceKey",
                table: "HospitalSpecialties",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "HospitalSpecialties",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "VerifiedOn",
                table: "HospitalSpecialties",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HospitalSpecialties_DataSourceKey",
                table: "HospitalSpecialties",
                column: "DataSourceKey");

            migrationBuilder.AddForeignKey(
                name: "FK_HospitalSpecialties_DataSources_DataSourceKey",
                table: "HospitalSpecialties",
                column: "DataSourceKey",
                principalTable: "DataSources",
                principalColumn: "Key",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HospitalSpecialties_DataSources_DataSourceKey",
                table: "HospitalSpecialties");

            migrationBuilder.DropIndex(
                name: "IX_HospitalSpecialties_DataSourceKey",
                table: "HospitalSpecialties");

            migrationBuilder.DropColumn(
                name: "DataSourceKey",
                table: "HospitalSpecialties");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "HospitalSpecialties");

            migrationBuilder.DropColumn(
                name: "VerifiedOn",
                table: "HospitalSpecialties");
        }
    }
}
