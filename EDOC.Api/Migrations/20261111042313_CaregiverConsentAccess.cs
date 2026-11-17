using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDOC.Api.Migrations
{
    /// <inheritdoc />
    public partial class CaregiverConsentAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResourceId",
                table: "AccountSecurityLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CaregiverLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InviteEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedInviteEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CaregiverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvitationTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InvitationExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConsentGrantedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CanViewAppointments = table.Column<bool>(type: "bit", nullable: false),
                    CanViewMedicalRecords = table.Column<bool>(type: "bit", nullable: false),
                    CanViewMedicationReminders = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaregiverLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaregiverLinks_AspNetUsers_CaregiverUserId",
                        column: x => x.CaregiverUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaregiverLinks_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[] { new Guid("e96f2bd0-170d-4c89-8bb5-e6d57086100e"), "a1f03f2b-9e89-4a93-b6aa-24889ef22a05", "Caregiver", "CAREGIVER" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountSecurityLogs_ResourceId_CreatedAt",
                table: "AccountSecurityLogs",
                columns: new[] { "ResourceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CaregiverLinks_CaregiverUserId_RevokedAt_AcceptedAt",
                table: "CaregiverLinks",
                columns: new[] { "CaregiverUserId", "RevokedAt", "AcceptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CaregiverLinks_InvitationTokenHash",
                table: "CaregiverLinks",
                column: "InvitationTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaregiverLinks_PatientId_NormalizedInviteEmail",
                table: "CaregiverLinks",
                columns: new[] { "PatientId", "NormalizedInviteEmail" },
                unique: true,
                filter: "[RevokedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaregiverLinks");

            migrationBuilder.DropIndex(
                name: "IX_AccountSecurityLogs_ResourceId_CreatedAt",
                table: "AccountSecurityLogs");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("e96f2bd0-170d-4c89-8bb5-e6d57086100e"));

            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "AccountSecurityLogs");
        }
    }
}
