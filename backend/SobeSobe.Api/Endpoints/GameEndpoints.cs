using Microsoft.EntityFrameworkCore;
using SobeSobe.Api.DTOs;
using SobeSobe.Api.Services.Realtime;
using SobeSobe.Core.Entities;
using SobeSobe.Core.Enums;
using SobeSobe.Infrastructure.Data;

namespace SobeSobe.Api.Endpoints;

public static class GameEndpoints
{
    public static IEndpointRouteBuilder MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        // List Games endpoint (public)
        app.MapGet("/api/games", async (
            ApplicationDbContext db,
            int? status,
            bool? availableOnly,
            Guid? createdBy,
            int page = 1,
            int pageSize = 20) =>
        {
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            // Build query
            var query = db.Games
                .Include(g => g.CreatedBy)
                .Include(g => g.PlayerSessions)
                    .ThenInclude(ps => ps.User)
                .AsQueryable();

            // Apply filters
            if (status.HasValue)
            {
                query = query.Where(g => (int)g.Status == status.Value);
            }
            else if (availableOnly == true)
            {
                query = query.Where(g => g.Status != GameStatus.Abandoned && g.Status != GameStatus.Completed);
            }

            if (createdBy.HasValue)
            {
                query = query.Where(g => g.CreatedByUserId == createdBy.Value);
            }

            // Order by creation date (newest first)
            query = query.OrderByDescending(g => g.CreatedAt);

            // Get total count
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Apply pagination
            var games = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Map to response
            var gameListItems = games.Select(g => new GameListItem
            {
                Id = g.Id,
                CreatedBy = new UserSummary
                {
                    Id = g.CreatedBy!.Id,
                    Username = g.CreatedBy.Username,
                    DisplayName = g.CreatedBy.DisplayName
                },
                Status = (int)g.Status,
                MaxPlayers = g.MaxPlayers,
                CurrentPlayers = g.PlayerSessions.Count,
                Players = g.PlayerSessions
                    .OrderBy(ps => ps.Position)
                    .Select(ps => new PlayerSummary
                    {
                        UserId = ps.User!.Id,
                        Username = ps.User.Username,
                        DisplayName = ps.User.DisplayName,
                        Position = ps.Position
                    })
                    .ToList(),
                CreatedAt = g.CreatedAt
            }).ToList();

            var response = new ListGamesResponse
            {
                Games = gameListItems,
                Pagination = new PaginationInfo
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages,
                    TotalItems = totalItems
                }
            };

            return Results.Ok(response);
        })
        .WithName("ListGames");

        // Get Game Details endpoint (public)
        app.MapGet("/api/games/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            // Find game with all related data
            var game = await db.Games
                .Include(g => g.CreatedBy)
                .Include(g => g.PlayerSessions)
                    .ThenInclude(ps => ps.User)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return Results.NotFound(new { error = "Game not found" });
            }

            // Map to response
            var gameResponse = new GameResponse
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
                Players = game.PlayerSessions
                    .OrderBy(ps => ps.Position)
                    .Select(ps => new PlayerSessionResponse
                    {
                        Id = ps.Id,
                        UserId = ps.User!.Id,
                        Username = ps.User.Username,
                        DisplayName = ps.User.DisplayName,
                        AvatarUrl = ps.User.AvatarUrl,
                        Position = ps.Position,
                        CurrentPoints = ps.CurrentPoints,
                        IsActive = ps.IsActive,
                        ConsecutiveRoundsOut = ps.ConsecutiveRoundsOut,
                        JoinedAt = ps.JoinedAt
                    })
                    .ToList()
            };

            return Results.Ok(gameResponse);
        })
        .WithName("GetGameDetails");

        // Create Game endpoint (requires authentication)
        app.MapPost("/api/games", async (CreateGameRequest request, HttpContext httpContext, ApplicationDbContext db,
            ILobbyEventBroadcaster lobbyEvents) =>
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

            // Create game
            var game = new Game
            {
                CreatedByUserId = userId,
                Status = GameStatus.Waiting,
                MaxPlayers = request.MaxPlayers,
                CurrentRoundNumber = 0,
                CreatedAt = DateTime.UtcNow
            };

            db.Games.Add(game);

            // Create player session for game creator (position 0)
            var playerSession = new PlayerSession
            {
                GameId = game.Id,
                UserId = userId,
                Position = 0,
                CurrentPoints = 20, // Starting points
                IsActive = true,
                ConsecutiveRoundsOut = 0,
                JoinedAt = DateTime.UtcNow
            };

            db.PlayerSessions.Add(playerSession);

            await db.SaveChangesAsync();

            await lobbyEvents.BroadcastLobbyListChangedAsync(game.Id.ToString());

            // Return game response
            var gameResponse = new GameResponse
            {
                Id = game.Id,
                CreatedBy = game.CreatedByUserId,
                CreatedByUsername = user.Username,
                Status = game.Status,
                MaxPlayers = game.MaxPlayers,
                CurrentPlayerCount = 1,
                CurrentDealerIndex = game.CurrentDealerPosition,
                CurrentRoundNumber = game.CurrentRoundNumber,
                CreatedAt = game.CreatedAt,
                StartedAt = game.StartedAt,
                CompletedAt = game.CompletedAt,
                Players = new List<PlayerSessionResponse>
                {
                    new PlayerSessionResponse
                    {
                        Id = playerSession.Id,
                        UserId = user.Id,
                        Username = user.Username,
                        DisplayName = user.DisplayName,
                        AvatarUrl = user.AvatarUrl,
                        Position = playerSession.Position,
                        CurrentPoints = playerSession.CurrentPoints,
                        IsActive = playerSession.IsActive,
                        ConsecutiveRoundsOut = playerSession.ConsecutiveRoundsOut,
                        JoinedAt = playerSession.JoinedAt
                    }
                }
            };

            return Results.Created($"/api/games/{game.Id}", gameResponse);
        })
        .RequireAuthorization()
        .WithName("CreateGame");

        // Join Game endpoint (requires authentication)
        app.MapPost("/api/games/{id:guid}/join", async (Guid id, HttpContext httpContext, ApplicationDbContext db,
            IGameEventBroadcaster gameEvents, ILobbyEventBroadcaster lobbyEvents) =>
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

            // Find the game with player sessions
            var game = await db.Games
                .Include(g => g.PlayerSessions)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return Results.NotFound(new { error = "Game not found" });
            }

            // Check if game has already started
            if (game.Status != GameStatus.Waiting)
            {
                return Results.BadRequest(new { error = "Game has already started" });
            }

            // Check if user is already in this game
            if (game.PlayerSessions.Any(ps => ps.UserId == userId))
            {
                return Results.Conflict(new { error = "User already in this game" });
            }

            // Check if game is full
            if (game.PlayerSessions.Count >= game.MaxPlayers)
            {
                return Results.BadRequest(new { error = "Game is full" });
            }

            // Find next available position
            var occupiedPositions = game.PlayerSessions.Select(ps => ps.Position).ToHashSet();
            int nextPosition = 0;
            while (occupiedPositions.Contains(nextPosition))
            {
                nextPosition++;
            }

            // Create player session for joining user
            var playerSession = new PlayerSession
            {
                GameId = game.Id,
                UserId = userId,
                Position = nextPosition,
                CurrentPoints = 20, // Starting points
                IsActive = true,
                ConsecutiveRoundsOut = 0,
                JoinedAt = DateTime.UtcNow
            };

            db.PlayerSessions.Add(playerSession);
            await db.SaveChangesAsync();

            // Broadcast player joined event
            await gameEvents.BroadcastPlayerJoinedAsync(
                game.Id.ToString(),
                user.Id.ToString(),
                user.Username,
                user.DisplayName,
                playerSession.Position);

            await lobbyEvents.BroadcastLobbyListChangedAsync(game.Id.ToString());

            // Return join response
            var joinResponse = new JoinGameResponse
            {
                GameId = game.Id,
                PlayerSession = new PlayerSessionResponse
                {
                    Id = playerSession.Id,
                    UserId = user.Id,
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    AvatarUrl = user.AvatarUrl,
                    Position = playerSession.Position,
                    CurrentPoints = playerSession.CurrentPoints,
                    IsActive = playerSession.IsActive,
                    ConsecutiveRoundsOut = playerSession.ConsecutiveRoundsOut,
                    JoinedAt = playerSession.JoinedAt
                }
            };

            return Results.Ok(joinResponse);
        })
        .RequireAuthorization()
        .WithName("JoinGame");

        // Leave Game endpoint (requires authentication)
        app.MapPost("/api/games/{id:guid}/leave", async (Guid id, HttpContext httpContext, ApplicationDbContext db,
            IGameEventBroadcaster gameEvents, ILobbyEventBroadcaster lobbyEvents) =>
        {
            // Get user ID from JWT claims
            var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            // Find game with player sessions
            var game = await db.Games
                .Include(g => g.PlayerSessions)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return Results.NotFound(new { error = "Game not found" });
            }

            // Check if game has already started
            if (game.Status != GameStatus.Waiting)
            {
                return Results.BadRequest(new { error = "Cannot leave game that has already started" });
            }

            // Find player's session in this game
            var playerSession = game.PlayerSessions.FirstOrDefault(ps => ps.UserId == userId);
            if (playerSession == null)
            {
                return Results.NotFound(new { error = "You are not in this game" });
            }

            // If the creator leaves before the game starts, delete the game and disassociate all players
            if (game.CreatedByUserId == userId)
            {
                await gameEvents.BroadcastGameAbandonedAsync(
                    game.Id.ToString(),
                    userId.ToString(),
                    "Game was deleted by the host before it started.");

                db.Games.Remove(game);
                await db.SaveChangesAsync();

                await lobbyEvents.BroadcastLobbyListChangedAsync(game.Id.ToString());

                return Results.Ok(new { message = "Game deleted successfully" });
            }

            // Remove player session
            playerSession.LeftAt = DateTime.UtcNow;
            var leavingPosition = playerSession.Position; // Store before removing
            db.PlayerSessions.Remove(playerSession);

            // If no players left, delete the game
            if (game.PlayerSessions.Count == 1) // Only this player left
            {
                db.Games.Remove(game);
            }

            await db.SaveChangesAsync();

            // Broadcast player left event (only if game still exists)
            if (game.PlayerSessions.Count > 1)
            {
                await gameEvents.BroadcastPlayerLeftAsync(
                    game.Id.ToString(),
                    userId.ToString(),
                    leavingPosition);
            }

            await lobbyEvents.BroadcastLobbyListChangedAsync(game.Id.ToString());

            return Results.Ok(new { message = "Left game successfully" });
        })
        .RequireAuthorization()
        .WithName("LeaveGame");

        // Cancel Game endpoint (requires authentication, creator only)
        app.MapDelete("/api/games/{id:guid}", async (Guid id, HttpContext httpContext, ApplicationDbContext db,
            ILobbyEventBroadcaster lobbyEvents) =>
        {
            // Get user ID from JWT claims
            var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            // Find game
            var game = await db.Games
                .Include(g => g.PlayerSessions)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return Results.NotFound(new { error = "Game not found" });
            }

            // Check if user is the game creator
            if (game.CreatedByUserId != userId)
            {
                return Results.StatusCode(403); // Forbidden
            }

            // Check if game has already started
            if (game.Status != GameStatus.Waiting)
            {
                return Results.BadRequest(new { error = "Cannot cancel game that has already started" });
            }

            // Delete the game (cascading delete will remove player sessions)
            db.Games.Remove(game);
            await db.SaveChangesAsync();

            await lobbyEvents.BroadcastLobbyListChangedAsync(game.Id.ToString());

            return Results.Ok(new { message = "Game cancelled successfully" });
        })
        .RequireAuthorization()
        .WithName("CancelGame");

        // Abandon Game endpoint (requires authentication, creator only)
        app.MapPost("/api/games/{id:guid}/abandon", async (Guid id, HttpContext httpContext, ApplicationDbContext db,
            IGameEventBroadcaster gameEvents, ILobbyEventBroadcaster lobbyEvents) =>
        {
            // Get user ID from JWT claims
            var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            // Find game with player sessions
            var game = await db.Games
                .Include(g => g.PlayerSessions)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return Results.NotFound(new { error = "Game not found" });
            }

            // Check if user is the game creator
            if (game.CreatedByUserId != userId)
            {
                return Results.StatusCode(403); // Forbidden
            }

            if (game.Status == GameStatus.Abandoned)
            {
                return Results.BadRequest(new { error = "Game is already abandoned" });
            }

            game.Status = GameStatus.Abandoned;
            game.CompletedAt ??= DateTime.UtcNow;

            await db.SaveChangesAsync();

            await gameEvents.BroadcastGameAbandonedAsync(
                game.Id.ToString(),
                userId.ToString(),
                "Game was abandoned by the host.");

            await lobbyEvents.BroadcastLobbyListChangedAsync(game.Id.ToString());

            return Results.Ok(new { message = "Game abandoned successfully" });
        })
        .RequireAuthorization()
        .WithName("AbandonGame");

        // Start Game endpoint (requires authentication, creator only)
        app.MapPost("/api/games/{id:guid}/start", async (Guid id, HttpContext httpContext, ApplicationDbContext db,
            IGameEventBroadcaster gameEvents, ILobbyEventBroadcaster lobbyEvents) =>
        {
            // Get user ID from JWT claims
            var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            // Find game with player sessions
            var game = await db.Games
                .Include(g => g.PlayerSessions)
                    .ThenInclude(ps => ps.User)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return Results.NotFound(new { error = "Game not found" });
            }

            // Check if user is the game creator
            if (game.CreatedByUserId != userId)
            {
                return Results.StatusCode(403); // Forbidden
            }

            // Check if game has already started
            if (game.Status != GameStatus.Waiting)
            {
                return Results.BadRequest(new { error = "Game has already started" });
            }

            // Check minimum player count (need at least 2 players)
            var activePlayers = game.PlayerSessions.Where(ps => ps.IsActive).ToList();
            if (activePlayers.Count < 2)
            {
                return Results.BadRequest(new { error = "Need at least 2 players to start the game" });
            }

            // Select random dealer from active players
            var random = new Random();
            var dealerIndex = random.Next(activePlayers.Count);
            var dealer = activePlayers[dealerIndex];

            // Party player is counter-clockwise from dealer (to the right)
            // Find next position counter-clockwise
            var dealerPosition = dealer.Position;
            var partyPlayerPosition = (dealerPosition + 1) % activePlayers.Count;

            // Find party player by position (need to handle cases where positions might not be sequential)
            var sortedPlayers = activePlayers.OrderBy(p => p.Position).ToList();
            var dealerIndexInSorted = sortedPlayers.FindIndex(p => p.Position == dealerPosition);
            var partyPlayerIndexInSorted = (dealerIndexInSorted + 1) % sortedPlayers.Count;
            var partyPlayer = sortedPlayers[partyPlayerIndexInSorted];

            // Create first round
            var round = new Round
            {
                GameId = game.Id,
                RoundNumber = 1,
                DealerUserId = dealer.UserId,
                PartyPlayerUserId = partyPlayer.UserId,
                Status = RoundStatus.TrumpSelection,
                TrumpSuit = TrumpSuit.Hearts, // Default, will be selected by party player
                TrumpSelectedBeforeDealing = false,
                TrickValue = 0, // Will be set after trump selection
                CurrentTrickNumber = 0,
                StartedAt = DateTime.UtcNow
            };

            db.Rounds.Add(round);

            // Update game status
            game.Status = GameStatus.InProgress;
            game.StartedAt = DateTime.UtcNow;
            game.CurrentRoundNumber = 1;
            game.CurrentDealerPosition = dealer.Position;

            await db.SaveChangesAsync();

            // Broadcast game started event
            await gameEvents.BroadcastGameStartedAsync(
                game.Id.ToString(),
                game.StartedAt.Value,
                dealer.Position,
                game.PlayerSessions.Select(ps => (
                    UserId: ps.UserId.ToString(),
                    Username: ps.User!.Username,
                    DisplayName: ps.User!.DisplayName,
                    Position: ps.Position,
                    Points: ps.CurrentPoints
                )).ToList());

            await lobbyEvents.BroadcastLobbyListChangedAsync(game.Id.ToString());

            // Return start game response
            var response = new StartGameResponse
            {
                GameId = game.Id,
                Status = game.Status.ToString(),
                StartedAt = game.StartedAt.Value,
                CurrentRoundNumber = game.CurrentRoundNumber,
                CurrentDealerPosition = game.CurrentDealerPosition.Value
            };

            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithName("StartGame");

        return app;
    }
}
