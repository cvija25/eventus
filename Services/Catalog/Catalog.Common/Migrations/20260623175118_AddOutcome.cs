using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Outcome",
                table: "Events",
                type: "integer",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Outcome", table: "Events");
        }
    }
}
