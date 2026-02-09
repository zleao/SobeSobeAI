using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SobeSobe.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoundDeck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Deck",
                table: "Rounds",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Deck",
                table: "Rounds");
        }
    }
}
