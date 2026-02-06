using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SobeSobe.Api.DTOs;
using SobeSobe.Api.Extensions;
using SobeSobe.Api.Services;
using SobeSobe.Core.Entities;
using SobeSobe.Core.Enums;
using SobeSobe.Core.ValueObjects;
using SobeSobe.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddAuthorization();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Prevent ASP.NET from remapping claim types (e.g., sub -> NameIdentifier)
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = JwtRegisteredClaimNames.UniqueName,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<TrickTakingService>();

builder.Services.AddGrpc();

var useInMemoryDatabase = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (useInMemoryDatabase)
    {
        options.UseInMemoryDatabase("SobeSobeTestDb");
    }
    else
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.UseSqlite(connectionString);
    }
});

var app = builder.Build();

// Ensure database exists and all migrations are applied on startup.
// This is important for local dev and self-hosted deployments where the SQLite file
// may be deleted between restarts.
if (!useInMemoryDatabase)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

// gRPC service
app.MapGrpcService<GameEventsService>();

app.MapPost("/api/users/register", async (RegisterUserRequest request, ApplicationDbContext db) =>
{
    var existingUsername = await db.Users.AnyAsync(u => u.Username == request.Username);
    if (existingUsername)
    {
        return Results.BadRequest(new { error = new { message = "Username already exists" } });
    }

    var existingEmail = await db.Users.AnyAsync(u => u.Email == request.Email);
    if (existingEmail)
    {
        return Results.BadRequest(new { error = new { message = "Email already exists" } });
    }

    var user = new User
    {
        Username = request.Username,
        Email = request.Email,
        DisplayName = request.DisplayName,
        PasswordHash = PasswordHasher.HashPassword(request.Password),
        AvatarUrl = null,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        LastLoginAt = null,
        GamesPlayed = 0,
        GamesWon = 0,
        TotalPrizeWon = 0m,
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    var response = new UserResponse
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        DisplayName = user.DisplayName,
        AvatarUrl = user.AvatarUrl,
        CreatedAt = user.CreatedAt
    };

    return Results.Created($"/api/users/{user.Id}", response);
})
.WithName("RegisterUser")
.WithOpenApi();

app.MapPost("/api/auth/login", async (LoginRequest request, ApplicationDbContext db, JwtTokenService jwtTokenService) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.UsernameOrEmail || u.Email == request.UsernameOrEmail);

    if (user == null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
    {
        return Results.BadRequest(new { error = new { message = "Invalid credentials" } });
    }

    user.LastLoginAt = DateTime.UtcNow;
    user.UpdatedAt = DateTime.UtcNow;

    var accessToken = jwtTokenService.GenerateAccessToken(user);
    var refreshToken = await jwtTokenService.GenerateAndStoreRefreshTokenAsync(user);

    await db.SaveChangesAsync();

    return Results.Ok(new LoginResponse
    {
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        TokenType = "Bearer",
        ExpiresIn = 900,
        User = new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            CreatedAt = user.CreatedAt
        }
    });
})
.WithName("Login")
.WithOpenApi();

app.MapPost("/api/auth/refresh", async (RefreshTokenRequest request, ApplicationDbContext db, JwtTokenService jwtTokenService) =>
{
    var tokenResult = await jwtTokenService.ValidateRefreshTokenAsync(request.RefreshToken);

    if (tokenResult.User == null)
    {
        return Results.BadRequest(new { error = new { message = tokenResult.ErrorMessage ?? "Invalid refresh token" } });
    }

    // revoke the old token and issue a new pair
    await jwtTokenService.RevokeRefreshTokenAsync(request.RefreshToken);

    var newAccessToken = jwtTokenService.GenerateAccessToken(tokenResult.User);
    var newRefreshToken = await jwtTokenService.GenerateAndStoreRefreshTokenAsync(tokenResult.User);

    await db.SaveChangesAsync();

    return Results.Ok(new RefreshTokenResponse
    {
        AccessToken = newAccessToken,
        RefreshToken = newRefreshToken,
        TokenType = "Bearer",
        ExpiresIn = 900
    });
})
.WithName("RefreshToken")
.WithOpenApi();

app.MapPost("/api/auth/logout", async (LogoutRequest request, JwtTokenService jwtTokenService) =>
{
    await jwtTokenService.RevokeRefreshTokenAsync(request.RefreshToken);

    return Results.Ok(new { message = "Logged out successfully" });
})
.WithName("Logout")
.WithOpenApi();

app.MapGet("/api/auth/user", async (HttpContext httpContext, ApplicationDbContext db) =>
{
    var userIdClaim = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub);

    if (userIdClaim == null)
    {
        return Results.Unauthorized();
    }

    if (!Guid.TryParse(userIdClaim.Value, out var userId))
    {
        return Results.Unauthorized();
    }

    var user = await db.Users.FindAsync(userId);

    if (user == null)
    {
        return Results.NotFound(new { error = new { message = "User not found" } });
    }

    return Results.Ok(new UserResponse
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        DisplayName = user.DisplayName,
        AvatarUrl = user.AvatarUrl,
        CreatedAt = user.CreatedAt
    });
})
.RequireAuthorization()
.WithName("GetCurrentUser")
.WithOpenApi();

app.MapGet("/api/games", async (
    ApplicationDbContext db,
    GameStatus? status,
    Guid? createdBy,
    int page = 1,
    int pageSize = 20) =>
{
    if (page < 1) page = 1;
    if (pageSize < 1 || pageSize > 100) pageSize = 20;

    var query = db.Games
        .Include(g => g.CreatedBy!)
        .Include(g => g.PlayerSessions)
            .ThenInclude(ps => ps.User!)
        .AsQueryable();

    if (status.HasValue)
    {
        query = query.Where(g => g.Status == status.Value);
    }

    if (createdBy.HasValue)
    {
        query = query.Where(g => g.CreatedByUserId == createdBy.Value);
    }

    var totalItems = await query.CountAsync();
    var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

    var games = await query
        .OrderByDescending(g => g.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    var response = new ListGamesResponse
    {
        Pagination = new PaginationMetadata
        {
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalItems = totalItems
        },
        Games = games.Select(g => new GameListItem
        {
            Id = g.Id,
            Status = g.Status,
            MaxPlayers = g.MaxPlayers,
            CurrentPlayers = g.PlayerSessions.Count,
            CreatedAt = g.CreatedAt,
            CreatedBy = new UserSummary
            {
                Id = g.CreatedBy!.Id,
                Username = g.CreatedBy.Username,
                DisplayName = g.CreatedBy.DisplayName
            },
            Players = g.PlayerSessions
                .OrderBy(ps => ps.Position)
                .Select(ps => new PlayerSummary
                {
                    Id = ps.User!.Id,
                    Username = ps.User.Username,
                    DisplayName = ps.User.DisplayName,
                    Position = ps.Position
                })
                .ToList()
        }).ToList()
    };

    return Results.Ok(response);
})
.WithName("ListGames")
.WithOpenApi();

app.MapPost("/api/games", async (HttpContext httpContext, CreateGameRequest request, ApplicationDbContext db) =>
{
    var userIdClaim = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub);

    if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
    {
        return Results.Unauthorized();
    }

    var user = await db.Users.FindAsync(userId);

    if (user == null)
    {
        return Results.NotFound(new { error = new { message = "User not found" } });
    }

    var game = new Game
    {
        CreatedByUserId = user.Id,
        Status = GameStatus.Waiting,
        MaxPlayers = request.MaxPlayers,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        StartedAt = null,
        CompletedAt = null,
        CurrentRoundNumber = 0,
        CurrentDealerPosition = 0
    };

    // Creator automatically joins as first player
    var creatorSession = new PlayerSession
    {
        Game = game,
        UserId = user.Id,
        Position = 0,
        CurrentPoints = 20,
        IsActive = true,
        ConsecutiveRoundsOut = 0,
        JoinedAt = DateTime.UtcNow,
        LeftAt = null
    };

    game.PlayerSessions.Add(creatorSession);

    db.Games.Add(game);
    await db.SaveChangesAsync();

    var response = new GameResponse
    {
        Id = game.Id,
        Status = game.Status,
        MaxPlayers = game.MaxPlayers,
        CreatedAt = game.CreatedAt,
        CreatedBy = game.CreatedByUserId,
        Players = game.PlayerSessions
            .OrderBy(ps => ps.Position)
            .Select(ps => new PlayerSessionResponse
            {
                Id = ps.Id,
                UserId = ps.UserId,
                Username = user.Username,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                Position = ps.Position,
                CurrentPoints = ps.CurrentPoints,
                ConsecutiveRoundsOut = ps.ConsecutiveRoundsOut,
                IsActive = ps.IsActive,
                JoinedAt = ps.JoinedAt,
                LeftAt = ps.LeftAt
            })
            .ToList()
    };

    return Results.Created($"/api/games/{game.Id}", response);
})
.RequireAuthorization()
.WithName("CreateGame")
.WithOpenApi();

app.MapGet("/api/games/{id:guid}", async (Guid id, ApplicationDbContext db) =>
{
    var game = await db.Games
        .Include(g => g.CreatedBy!)
        .Include(g => g.PlayerSessions)
            .ThenInclude(ps => ps.User!)
        .FirstOrDefaultAsync(g => g.Id == id);

    if (game == null)
    {
        return Results.NotFound(new { error = new { message = "Game not found" } });
    }

    var response = new GameResponse
    {
        Id = game.Id,
        Status = game.Status,
        MaxPlayers = game.MaxPlayers,
        CreatedAt = game.CreatedAt,
        CreatedBy = game.CreatedByUserId,
        Players = game.PlayerSessions
            .OrderBy(ps => ps.Position)
            .Select(ps => new PlayerSessionResponse
            {
                Id = ps.Id,
                UserId = ps.UserId,
                Username = ps.User!.Username,
                DisplayName = ps.User.DisplayName,
                AvatarUrl = ps.User.AvatarUrl,
                Position = ps.Position,
                CurrentPoints = ps.CurrentPoints,
                ConsecutiveRoundsOut = ps.ConsecutiveRoundsOut,
                IsActive = ps.IsActive,
                JoinedAt = ps.JoinedAt,
                LeftAt = ps.LeftAt
            })
            .ToList()
    };

    return Results.Ok(response);
})
.WithName("GetGame")
.WithOpenApi();

app.MapPost("/api/games/{id:guid}/join", async (Guid id, HttpContext httpContext, ApplicationDbContext db) =>
{
    var userIdClaim = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub);

    if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
    {
        return Results.Unauthorized();
    }

    var user = await db.Users.FindAsync(userId);

    if (user == null)
    {
        return Results.NotFound(new { error = new { message = "User not found" } });
    }

    var game = await db.Games
        .Include(g => g.PlayerSessions)
        .FirstOrDefaultAsync(g => g.Id == id);

    if (game == null)
    {
        return Results.NotFound(new { error = new { message = "Game not found" } });
    }

    if (game.Status != GameStatus.Waiting)
    {
        return Results.BadRequest(new { error = new { message = "Cannot join a game that has already started" } });
    }

    if (game.PlayerSessions.Any(ps => ps.UserId == user.Id && ps.LeftAt == null))
    {
        return Results.Conflict(new { error = new { message = "User is already in the game" } });
    }

    if (game.PlayerSessions.Count(ps => ps.LeftAt == null) >= game.MaxPlayers)
    {
        return Results.BadRequest(new { error = new { message = "Game is full" } });
    }

    var occupiedPositions = game.PlayerSessions
        .Where(ps => ps.LeftAt == null)
        .Select(ps => ps.Position)
        .ToHashSet();

    var position = Enumerable.Range(0, 5).First(p => !occupiedPositions.Contains(p));

    var session = new PlayerSession
    {
        GameId = game.Id,
        UserId = user.Id,
        Position = position,
        CurrentPoints = 20,
        IsActive = true,
        ConsecutiveRoundsOut = 0,
        JoinedAt = DateTime.UtcNow,
        LeftAt = null
    };

    db.PlayerSessions.Add(session);
    await db.SaveChangesAsync();

    await GameEventExtensions.BroadcastPlayerJoinedAsync(game.Id, user.Id, user.Username, user.DisplayName, position);

    return Results.Ok(new JoinGameResponse
    {
        GameId = game.Id,
        PlayerSession = new PlayerSessionResponse
        {
            Id = session.Id,
            UserId = session.UserId,
            Username = user.Username,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            Position = session.Position,
            CurrentPoints = session.CurrentPoints,
            ConsecutiveRoundsOut = session.ConsecutiveRoundsOut,
            IsActive = session.IsActive,
            JoinedAt = session.JoinedAt,
            LeftAt = session.LeftAt
        }
    });
})
.RequireAuthorization()
.WithName("JoinGame")
.WithOpenApi();

app.MapPost("/api/games/{id:guid}/leave", async (Guid id, HttpContext httpContext, ApplicationDbContext db) =>
{
    var userIdClaim = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub);

    if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
    {
        return Results.Unauthorized();
    }

    var game = await db.Games
        .Include(g => g.PlayerSessions)
        .FirstOrDefaultAsync(g => g.Id == id);

    if (game == null)
    {
        return Results.NotFound(new { error = new { message = "Game not found" } });
    }

    if (game.Status != GameStatus.Waiting)
    {
        return Results.BadRequest(new { error = new { message = "Cannot leave a game that has already started" } });
    }

    var session = game.PlayerSessions.FirstOrDefault(ps => ps.UserId == userId && ps.LeftAt == null);

    if (session == null)
    {
        return Results.NotFound(new { error = new { message = "User is not in the game" } });
    }

    session.LeftAt = DateTime.UtcNow;

    // If leaving user is creator, transfer ownership to next player
    if (game.CreatedByUserId == userId)
    {
        var remainingSessions = game.PlayerSessions
            .Where(ps => ps.UserId != userId && ps.LeftAt == null)
            .OrderBy(ps => ps.Position)
            .ToList();

        if (remainingSessions.Any())
        {
            game.CreatedByUserId = remainingSessions.First().UserId;
        }
    }

    // If last player leaves, delete the game
    var remainingPlayersCount = game.PlayerSessions.Count(ps => ps.LeftAt == null && ps.UserId != userId);

    db.PlayerSessions.Remove(session);

    if (remainingPlayersCount == 0)
    {
        db.Games.Remove(game);
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Left game successfully" });
    }

    await db.SaveChangesAsync();

    // Broadcast only if game still exists (not deleted when last player leaves)
    await GameEventExtensions.BroadcastPlayerLeftAsync(game.Id, userId);

    return Results.Ok(new { message = "Left game successfully" });
})
.RequireAuthorization()
.WithName("LeaveGame")
.WithOpenApi();

app.MapDelete("/api/games/{id:guid}", async (Guid id, HttpContext httpContext, ApplicationDbContext db) =>
{
    var userIdClaim = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub);

    if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
    {
        return Results.Unauthorized();
    }

    var game = await db.Games
        .Include(g => g.PlayerSessions)
        .FirstOrDefaultAsync(g => g.Id == id);

    if (game == null)
    {
        return Results.NotFound(new { error = new { message = "Game not found" } });
    }

    if (game.CreatedByUserId != userId)
    {
        return Results.Json(new { error = new { message = "Only the creator can cancel the game" } }, statusCode: 403);
    }

    if (game.Status != GameStatus.Waiting)
    {
        return Results.BadRequest(new { error = new { message = "Cannot cancel a game that has already started" } });
    }

    db.Games.Remove(game);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Game cancelled successfully" });
})
.RequireAuthorization()
.WithName("CancelGame")
.WithOpenApi();

app.MapPost("/api/games/{id:guid}/start", async (Guid id, HttpContext httpContext, ApplicationDbContext db) =>
{
    var userIdClaim = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub);

    if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
    {
        return Results.Unauthorized();
    }

    var game = await db.Games
        .Include(g => g.PlayerSessions)
        .FirstOrDefaultAsync(g => g.Id == id);

    if (game == null)
    {
        return Results.NotFound(new { error = new { message = "Game not found" } });
    }

    if (game.CreatedByUserId != userId)
    {
        return Results.Json(new { error = new { message = "Only the creator can start the game" } }, statusCode: 403);
    }

    if (game.Status != GameStatus.Waiting)
    {
        return Results.BadRequest(new { error = new { message = "Game has already started" } });
    }

    var activePlayers = game.PlayerSessions.Where(ps => ps.LeftAt == null).ToList();

    if (activePlayers.Count < 2)
    {
        return Results.BadRequest(new { error = new { message = "At least 2 players are required to start the game" } });
    }

    // Randomly select dealer
    var random = new Random();
    var dealerIndex = random.Next(activePlayers.Count);
    var dealerSession = activePlayers.OrderBy(ps => ps.Position).ToList()[dealerIndex];

    // Party player is counter-clockwise from dealer
    var sortedPlayers = activePlayers.OrderBy(ps => ps.Position).ToList();
    var dealerPositionIndex = sortedPlayers.FindIndex(ps => ps.Position == dealerSession.Position);
    var partyPlayerIndex = (dealerPositionIndex + 1) % sortedPlayers.Count;
    var partyPlayerSession = sortedPlayers[partyPlayerIndex];

    var round = new Round
    {
        GameId = game.Id,
        RoundNumber = 1,
        DealerUserId = dealerSession.UserId,
        PartyPlayerUserId = partyPlayerSession.UserId,
        Status = RoundStatus.TrumpSelection,
        TrumpSuit = TrumpSuit.Hearts,
        TrumpSelectedBeforeDealing = null,
        TrickValue = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        CompletedAt = null
    };

    game.Status = GameStatus.InProgress;
    game.StartedAt = DateTime.UtcNow;
    game.UpdatedAt = DateTime.UtcNow;
    game.CurrentRoundNumber = 1;
    game.CurrentDealerPosition = dealerSession.Position;

    db.Rounds.Add(round);
    await db.SaveChangesAsync();

    // Load users for event broadcast
    var playersWithUsers = await db.PlayerSessions
        .Where(ps => ps.GameId == game.Id && ps.LeftAt == null)
        .Include(ps => ps.User!)
        .ToListAsync();

    await GameEventExtensions.BroadcastGameStartedAsync(
        game.Id,
        playersWithUsers.Select(ps => (ps.Position, ps.UserId, ps.User!.Username, ps.User.DisplayName, ps.CurrentPoints)).ToList(),
        dealerSession.Position,
        partyPlayerSession.Position);

    return Results.Ok(new StartGameResponse
    {
        GameId = game.Id,
        Status = game.Status.ToString(),
        StartedAt = game.StartedAt.Value,
        CurrentRoundNumber = game.CurrentRoundNumber,
        CurrentDealerPosition = game.CurrentDealerPosition
    });
})
.RequireAuthorization()
.WithName("StartGame")
.WithOpenApi();

// ... (rest of endpoints unchanged)

app.Run();

public partial class Program { }
