using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadFinder.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContactsEditedByUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ContactsEditedByUser",
                table: "Leads",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactsEditedByUser",
                table: "Leads");
        }
    }
}
