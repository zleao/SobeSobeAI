using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SobeSobe.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AvatarUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalGamesPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalWins = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPointsScored = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPrizeWon = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxPlayers = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentDealerPosition = table.Column<int>(type: "INTEGER", nullable: true),
                    CurrentRoundNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WinnerUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Games_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Games_Users_WinnerUserId",
                        column: x => x.WinnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConsecutiveRoundsOut = table.Column<int>(type: "INTEGER", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LeftAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerSessions_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoundNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    DealerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyPlayerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrumpSuit = table.Column<int>(type: "INTEGER", nullable: false),
                    TrumpSelectedBeforeDealing = table.Column<bool>(type: "INTEGER", nullable: false),
                    TrickValue = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentTrickNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rounds_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rounds_Users_DealerUserId",
                        column: x => x.DealerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rounds_Users_PartyPlayerUserId",
                        column: x => x.PartyPlayerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Hands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoundId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Cards = table.Column<string>(type: "TEXT", nullable: false),
                    InitialCards = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hands_PlayerSessions_PlayerSessionId",
                        column: x => x.PlayerSessionId,
                        principalTable: "PlayerSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Hands_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScoreHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoundId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PointsChange = table.Column<int>(type: "INTEGER", nullable: false),
                    PointsAfter = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoreHistories_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScoreHistories_PlayerSessions_PlayerSessionId",
                        column: x => x.PlayerSessionId,
                        principalTable: "PlayerSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScoreHistories_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tricks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoundId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrickNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    LeadPlayerSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WinnerPlayerSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CardsPlayed = table.Column<string>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tricks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tricks_PlayerSessions_LeadPlayerSessionId",
                        column: x => x.LeadPlayerSessionId,
                        principalTable: "PlayerSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tricks_PlayerSessions_WinnerPlayerSessionId",
                        column: x => x.WinnerPlayerSessionId,
                        principalTable: "PlayerSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tricks_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_CreatedAt",
                table: "Games",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Games_CreatedByUserId",
                table: "Games",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Status",
                table: "Games",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Games_WinnerUserId",
                table: "Games",
                column: "WinnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Hands_PlayerSessionId",
                table: "Hands",
                column: "PlayerSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Hands_RoundId_PlayerSessionId",
                table: "Hands",
                columns: new[] { "RoundId", "PlayerSessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessions_GameId_Position",
                table: "PlayerSessions",
                columns: new[] { "GameId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessions_GameId_UserId",
                table: "PlayerSessions",
                columns: new[] { "GameId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessions_UserId",
                table: "PlayerSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_DealerUserId",
                table: "Rounds",
                column: "DealerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_GameId_RoundNumber",
                table: "Rounds",
                columns: new[] { "GameId", "RoundNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_PartyPlayerUserId",
                table: "Rounds",
                column: "PartyPlayerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_Status",
                table: "Rounds",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreHistories_CreatedAt",
                table: "ScoreHistories",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreHistories_GameId",
                table: "ScoreHistories",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreHistories_PlayerSessionId",
                table: "ScoreHistories",
                column: "PlayerSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreHistories_RoundId",
                table: "ScoreHistories",
                column: "RoundId");

            migrationBuilder.CreateIndex(
                name: "IX_Tricks_LeadPlayerSessionId",
                table: "Tricks",
                column: "LeadPlayerSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Tricks_RoundId_TrickNumber",
                table: "Tricks",
                columns: new[] { "RoundId", "TrickNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tricks_WinnerPlayerSessionId",
                table: "Tricks",
                column: "WinnerPlayerSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Hands");

            migrationBuilder.DropTable(
                name: "ScoreHistories");

            migrationBuilder.DropTable(
                name: "Tricks");

            migrationBuilder.DropTable(
                name: "PlayerSessions");

            migrationBuilder.DropTable(
                name: "Rounds");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
