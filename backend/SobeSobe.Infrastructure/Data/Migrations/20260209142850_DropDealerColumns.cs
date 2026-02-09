using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SobeSobe.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropDealerColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rounds_Users_DealerUserId",
                table: "Rounds");

            migrationBuilder.DropIndex(
                name: "IX_Rounds_DealerUserId",
                table: "Rounds");

            migrationBuilder.DropColumn(
                name: "DealerUserId",
                table: "Rounds");

            migrationBuilder.DropColumn(
                name: "CurrentDealerPosition",
                table: "Games");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DealerUserId",
                table: "Rounds",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "CurrentDealerPosition",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_DealerUserId",
                table: "Rounds",
                column: "DealerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rounds_Users_DealerUserId",
                table: "Rounds",
                column: "DealerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
