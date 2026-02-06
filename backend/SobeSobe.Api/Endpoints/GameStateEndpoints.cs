using Microsoft.EntityFrameworkCore;
using SobeSobe.Api.DTOs;
using SobeSobe.Core.Enums;
using SobeSobe.Core.ValueObjects;
using SobeSobe.Infrastructure.Data;
using System.Text.Json;

namespace SobeSobe.Api.Endpoints;

public static class GameStateEndpoints
{
    public static IEndpointRouteBuilder MapGameStateEndpoints(this IEndpointRouteBuilder app)
    {
        // Get Game State endpoint (requires authentication)
        app.MapGet("/api/games/{id:guid}/state", async (Guid id, HttpContext httpContext, ApplicationDbContext db) =>
        {
            // Get user ID from JWT claims
            var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            // Verify user exists
            var user = await db.Users.FindAsync(userId);
            if (user == null)
            {
                return Results.NotFound(new { error = "User not found" });
            }

            // Find the game with related entities
            var game = await db.Games
                .Include(g => g.CreatedBy)
                .Include(g => g.PlayerSessions)
                    .ThenInclude(ps => ps.User)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return Results.NotFound(new { error = "Game not found" });
            }

            // Find the requesting player's session
            var requestingPlayerSession = game.PlayerSessions.FirstOrDefault(ps => ps.UserId == userId);
            if (requestingPlayerSession == null)
            {
                return Results.Json(new { error = "You are not a player in this game" }, statusCode: 403);
            }

            // Build player state responses
            var playerStates = game.PlayerSessions
                .OrderBy(ps => ps.Position)
                .Select(ps => new PlayerStateResponse
                {
                    Id = ps.Id,
                    UserId = ps.UserId,
                    Username = ps.User!.Username,
                    DisplayName = ps.User!.DisplayName,
                    AvatarUrl = ps.User!.AvatarUrl,
                    Position = ps.Position,
                    CurrentPoints = ps.CurrentPoints,
                    IsActive = ps.IsActive,
                    ConsecutiveRoundsOut = ps.ConsecutiveRoundsOut,
                    JoinedAt = ps.JoinedAt,
                    LeftAt = ps.LeftAt,
                    Hand = null // Will be populated below for requesting player
                })
                .ToList();

            // Get current round if game is in progress
            RoundStateResponse? currentRoundState = null;
            if (game.Status == GameStatus.InProgress)
            {
                var currentRound = await db.Rounds
                    .Include(r => r.Tricks)
                    .Include(r => r.Hands)
                    .Where(r => r.GameId == id)
                    .OrderByDescending(r => r.RoundNumber)
                    .FirstOrDefaultAsync();

                if (currentRound == null)
                {
                    return Results.NotFound(new { error = "No active round found" });
                }

                // Get tricks for current round
                var tricks = currentRound.Tricks
                    .OrderBy(t => t.TrickNumber)
                    .Select(t => new TrickStateResponse
                    {
                        Id = t.Id,
                        TrickNumber = t.TrickNumber,
                        LeadPlayerSessionId = t.LeadPlayerSessionId,
                        WinnerPlayerSessionId = t.WinnerPlayerSessionId,
                        CardsPlayed = JsonSerializer.Deserialize<List<CardPlayedResponse>>(t.CardsPlayedJson) ?? [],
                        CompletedAt = t.CompletedAt
                    })
                    .ToList();

                // Find current (incomplete) trick
                var currentTrick = tricks.FirstOrDefault(t => t.CompletedAt == null);

                currentRoundState = new RoundStateResponse
                {
                    Id = currentRound.Id,
                    RoundNumber = currentRound.RoundNumber,
                    DealerUserId = currentRound.DealerUserId,
                    PartyPlayerUserId = currentRound.PartyPlayerUserId,
                    TrumpSuit = currentRound.TrumpSuit,
                    TrumpSelectedBeforeDealing = currentRound.TrumpSelectedBeforeDealing,
                    TrickValue = currentRound.TrickValue,
                    CurrentTrickNumber = currentRound.CurrentTrickNumber,
                    Status = currentRound.Status,
                    StartedAt = currentRound.StartedAt,
                    CompletedAt = currentRound.CompletedAt,
                    Tricks = tricks,
                    CurrentTrick = currentTrick
                };

                // Populate requesting player's hand
                var requestingPlayerHand = currentRound.Hands
                    .FirstOrDefault(h => h.PlayerSessionId == requestingPlayerSession.Id);

                if (requestingPlayerHand != null)
                {
                    var cards = JsonSerializer.Deserialize<List<Card>>(requestingPlayerHand.CardsJson);
                    var requestingPlayerState = playerStates.FirstOrDefault(ps => ps.Id == requestingPlayerSession.Id);
                    if (requestingPlayerState != null)
                    {
                        requestingPlayerState.Hand = cards;
                    }
                }
            }

            // Build game state response
            var gameStateResponse = new GameStateResponse
            {
                Id = game.Id,
                CreatedBy = game.CreatedByUserId,
                CreatedByUsername = game.CreatedBy!.Username,
                Status = game.Status,
                MaxPlayers = game.MaxPlayers,
                CurrentPlayerCount = game.PlayerSessions.Count,
                CurrentDealerIndex = game.CurrentDealerPosition,
                CurrentRoundNumber = game.CurrentRoundNumber,
                CreatedAt = game.CreatedAt,
                StartedAt = game.StartedAt,
                CompletedAt = game.CompletedAt,
                Players = playerStates,
                CurrentRound = currentRoundState
            };

            return Results.Ok(gameStateResponse);
        })
        .RequireAuthorization()
        .WithName("GetGameState");

        // Get Score History endpoint (requires authentication)
        app.MapGet("/api/games/{id:guid}/scores", async (Guid id, HttpContext httpContext, ApplicationDbContext db) =>
        {
            // Get user ID from JWT claims
            var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            // Verify user exists
            var user = await db.Users.FindAsync(userId);
            if (user == null)
            {
                return Results.NotFound(new { error = "User not found" });
            }

            // Find the game
            var game = await db.Games
                .Include(g => g.PlayerSessions)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return Results.NotFound(new { error = "Game not found" });
            }

            // Verify requesting user is a player in the game
            var requestingPlayerSession = game.PlayerSessions.FirstOrDefault(ps => ps.UserId == userId);
            if (requestingPlayerSession == null)
            {
                return Results.Json(new { error = "You are not a player in this game" }, statusCode: 403);
            }

            // Get all score history for this game, ordered chronologically
            var scoreHistory = await db.ScoreHistories
                .Include(sh => sh.PlayerSession!)
                    .ThenInclude(ps => ps.User!)
                .Include(sh => sh.Round)
                .Where(sh => sh.GameId == id)
                .OrderBy(sh => sh.CreatedAt)
                .Select(sh => new ScoreEntry
                {
                    Id = sh.Id,
                    GameId = sh.GameId,
                    PlayerSessionId = sh.PlayerSessionId,
                    PlayerPosition = sh.PlayerSession!.Position,
                    PlayerUsername = sh.PlayerSession!.User!.Username,
                    PlayerDisplayName = sh.PlayerSession!.User!.DisplayName,
                    RoundId = sh.RoundId,
                    RoundNumber = sh.Round != null ? sh.Round.RoundNumber : null,
                    PointsChange = sh.PointsChange,
                    PointsAfter = sh.PointsAfter,
                    Reason = sh.Reason,
                    CreatedAt = sh.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new ScoreHistoryResponse
            {
                Scores = scoreHistory
            });
        })
        .RequireAuthorization()
        .WithName("GetScoreHistory");

        return app;
    }
}
