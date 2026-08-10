using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketOutcomePots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PotSizeNo",
                table: "Events",
                type: "numeric",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "PotSizeYes",
                table: "Events",
                type: "numeric",
                nullable: false,
                defaultValue: 1m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PotSizeNo",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "PotSizeYes",
                table: "Events");
        }
    }
}
