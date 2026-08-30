using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Account.Migrations
{
    /// <inheritdoc />
    public partial class ReserveFunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReserveFunds",
                table: "Wallets",
                type: "numeric",
                nullable: false,
                defaultValue: 0m
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReserveFunds", table: "Wallets");
        }
    }
}
