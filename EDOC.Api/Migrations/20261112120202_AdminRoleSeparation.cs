using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EDOC.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdminRoleSeparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { new Guid("e96f2bd0-170d-4c89-8bb5-e6d57086100f"), "a1f03f2b-9e89-4a93-b6aa-24889ef22a06", "SuperAdmin", "SUPERADMIN" },
                    { new Guid("e96f2bd0-170d-4c89-8bb5-e6d570861010"), "a1f03f2b-9e89-4a93-b6aa-24889ef22a07", "HospitalAdmin", "HOSPITALADMIN" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("e96f2bd0-170d-4c89-8bb5-e6d57086100f"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("e96f2bd0-170d-4c89-8bb5-e6d570861010"));
        }
    }
}
