using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadFinder.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class NextActionDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "NextActionDate",
                table: "Leads",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextActionDate",
                table: "Leads");
        }
    }
}
