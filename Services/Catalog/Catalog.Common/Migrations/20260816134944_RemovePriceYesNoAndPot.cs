using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Common.Migrations
{
    /// <inheritdoc />
    public partial class RemovePriceYesNoAndPot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PotSize", table: "Events");

            migrationBuilder.DropColumn(name: "PriceNo", table: "Events");

            migrationBuilder.DropColumn(name: "PriceYes", table: "Events");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PotSize",
                table: "Events",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "PriceNo",
                table: "Events",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "PriceYes",
                table: "Events",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );
        }
    }
}
