using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadFinder.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegionScansAndUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FreeMonthlyRequests",
                table: "Settings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApiRequests",
                table: "SearchRuns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CityCount",
                table: "SearchRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "MinScore",
                table: "SearchRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 1,
                column: "FreeMonthlyRequests",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreeMonthlyRequests",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ApiRequests",
                table: "SearchRuns");

            migrationBuilder.DropColumn(
                name: "CityCount",
                table: "SearchRuns");

            migrationBuilder.DropColumn(
                name: "MinScore",
                table: "SearchRuns");
        }
    }
}
