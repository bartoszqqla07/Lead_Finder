using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadFinder.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class LeadScoringSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessStatus",
                table: "Leads",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CopyrightYear",
                table: "Leads",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMobileFriendly",
                table: "Leads",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsesHttps",
                table: "Leads",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessStatus",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "CopyrightYear",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "IsMobileFriendly",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "UsesHttps",
                table: "Leads");
        }
    }
}
