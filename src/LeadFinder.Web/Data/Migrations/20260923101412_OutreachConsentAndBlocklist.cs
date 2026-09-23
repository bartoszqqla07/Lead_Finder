using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadFinder.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class OutreachConsentAndBlocklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalAddress",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentGivenAt",
                table: "Leads",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BlockedPlaces",
                columns: table => new
                {
                    PlaceId = table.Column<string>(type: "TEXT", nullable: false),
                    BlockedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedPlaces", x => x.PlaceId);
                });

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ContactEmail", "PostalAddress" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedPlaces");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PostalAddress",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ConsentGivenAt",
                table: "Leads");
        }
    }
}
