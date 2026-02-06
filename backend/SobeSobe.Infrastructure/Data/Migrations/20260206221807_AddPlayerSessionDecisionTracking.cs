using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SobeSobe.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerSessionDecisionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastDecisionRoundNumber",
                table: "PlayerSessions",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastDecisionRoundNumber",
                table: "PlayerSessions");
        }
    }
}
