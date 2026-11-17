using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDOC.Api.Migrations
{
    /// <inheritdoc />
    public partial class RecurringMedicationReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastDoseStatus",
                table: "MedicationReminders",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepeatIntervalDays",
                table: "MedicationReminders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "MedicationReminders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastDoseStatus",
                table: "MedicationReminders");

            migrationBuilder.DropColumn(
                name: "RepeatIntervalDays",
                table: "MedicationReminders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MedicationReminders");
        }
    }
}
