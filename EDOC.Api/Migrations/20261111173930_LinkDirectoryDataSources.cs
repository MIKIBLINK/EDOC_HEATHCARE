using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDOC.Api.Migrations
{
    /// <inheritdoc />
    public partial class LinkDirectoryDataSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataSources_Key",
                table: "DataSources");

            migrationBuilder.AddColumn<string>(
                name: "DataSourceKey",
                table: "NssfVerificationRecords",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceKey",
                table: "Hospitals",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_DataSources_Key",
                table: "DataSources",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_NssfVerificationRecords_DataSourceKey",
                table: "NssfVerificationRecords",
                column: "DataSourceKey");

            migrationBuilder.CreateIndex(
                name: "IX_Hospitals_DataSourceKey",
                table: "Hospitals",
                column: "DataSourceKey");

            migrationBuilder.AddForeignKey(
                name: "FK_Hospitals_DataSources_DataSourceKey",
                table: "Hospitals",
                column: "DataSourceKey",
                principalTable: "DataSources",
                principalColumn: "Key",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NssfVerificationRecords_DataSources_DataSourceKey",
                table: "NssfVerificationRecords",
                column: "DataSourceKey",
                principalTable: "DataSources",
                principalColumn: "Key",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hospitals_DataSources_DataSourceKey",
                table: "Hospitals");

            migrationBuilder.DropForeignKey(
                name: "FK_NssfVerificationRecords_DataSources_DataSourceKey",
                table: "NssfVerificationRecords");

            migrationBuilder.DropIndex(
                name: "IX_NssfVerificationRecords_DataSourceKey",
                table: "NssfVerificationRecords");

            migrationBuilder.DropIndex(
                name: "IX_Hospitals_DataSourceKey",
                table: "Hospitals");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_DataSources_Key",
                table: "DataSources");

            migrationBuilder.DropColumn(
                name: "DataSourceKey",
                table: "NssfVerificationRecords");

            migrationBuilder.DropColumn(
                name: "DataSourceKey",
                table: "Hospitals");

            migrationBuilder.CreateIndex(
                name: "IX_DataSources_Key",
                table: "DataSources",
                column: "Key",
                unique: true);
        }
    }
}
