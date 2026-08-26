using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Common.Migrations
{
    /// <inheritdoc />
    public partial class PotAndPools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Pot",
                table: "Events",
                type: "numeric",
                nullable: false,
                defaultValue: 1m
            );

            migrationBuilder.AddColumn<decimal>(
                name: "PoolYes",
                table: "Events",
                type: "numeric",
                nullable: false,
                defaultValue: 1m
            );

            migrationBuilder.AddColumn<decimal>(
                name: "PoolNo",
                table: "Events",
                type: "numeric",
                nullable: false,
                defaultValue: 1m
            );

            migrationBuilder.DropColumn(name: "PotSizeYes", table: "Events");

            migrationBuilder.DropColumn(name: "PotSizeNo", table: "Events");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PotSizeYes",
                table: "Events",
                type: "numeric",
                nullable: false,
                defaultValue: 1m
            );

            migrationBuilder.AddColumn<decimal>(
                name: "PotSizeNo",
                table: "Events",
                type: "numeric",
                nullable: false,
                defaultValue: 1m
            );

            migrationBuilder.DropColumn(name: "Pot", table: "Events");

            migrationBuilder.DropColumn(name: "PoolYes", table: "Events");

            migrationBuilder.DropColumn(name: "PoolNo", table: "Events");
        }
    }
}
