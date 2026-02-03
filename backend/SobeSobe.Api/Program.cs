using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using SobeSobe.Infrastructure.Data;
using SobeSobe.Api.DTOs;
using SobeSobe.Api.Options;
using SobeSobe.Api.Services;
using SobeSobe.Api.Extensions;
using SobeSobe.Core.Entities;
using SobeSobe.Core.Enums;
using SobeSobe.Core.ValueObjects;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add database context
var useInMemoryDatabase = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");
if (useInMemoryDatabase)
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("TestDatabase"));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=sobesobe.db"));
}


// Configure JWT options
var jwtOptionsSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptions = jwtOptionsSection.Get<JwtOptions>() ?? throw new InvalidOperationException("JWT configuration section is missing");

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(jwtOptionsSection)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Add JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            // Map inbound claims to original JWT claim names
            NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName,
            RoleClaimType = "role"
        };

        // Don't map claims automatically
        options.MapInboundClaims = false;
    });

builder.Services.AddAuthorization();

// Register services
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<TrickTakingService>();

// Add gRPC services
builder.Services.AddGrpc();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Map Aspire service defaults (health checks)
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

// Map gRPC services
app.MapGrpcService<GameEventsService>();

// User Login endpoint
app.MapPost("/api/auth/login", async (LoginRequest request, ApplicationDbContext db, JwtTokenService jwtService) =>
{
    // Find user by username or email
    var user = await db.Users.FirstOrDefaultAsync(u =>
        u.Username == request.UsernameOrEmail || u.Email == request.UsernameOrEmail);

    if (user == null)
    {
        return Results.Unauthorized();
    }

    // Verify password
    if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    // Update last login timestamp
    user.LastLoginAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    // Generate tokens
    var accessToken = jwtService.GenerateAccessToken(user.Id, user.Username, user.Email);
    var refreshToken = await jwtService.GenerateAndStoreRefreshTokenAsync(user.Id);

    // Return login response
    var response = new LoginResponse
    {
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        TokenType = "Bearer",
        User = new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            CreatedAt = user.CreatedAt,
            TotalGamesPlayed = user.TotalGamesPlayed,
            TotalWins = user.TotalWins,
            TotalPrizeWon = user.TotalPrizeWon
        }
    };

    return Results.Ok(response);
})
.WithName("LoginUser");

// Token Refresh endpoint
app.MapPost("/api/auth/refresh", async (RefreshTokenRequest request, JwtTokenService jwtService) =>
{
    // Validate refresh token
    var refreshToken = await jwtService.ValidateRefreshTokenAsync(request.RefreshToken);
    
    if (refreshToken == null)
    {
        return Results.Unauthorized();
    }

    // Revoke old refresh token
    await jwtService.RevokeRefreshTokenAsync(request.RefreshToken);

    // Generate new tokens
    var user = refreshToken.User;
    var newAccessToken = jwtService.GenerateAccessToken(user.Id, user.Username, user.Email);
    var newRefreshToken = await jwtService.GenerateAndStoreRefreshTokenAsync(user.Id);

    // Return new tokens
    var response = new RefreshTokenResponse
    {
        AccessToken = newAccessToken,
        RefreshToken = newRefreshToken,
        TokenType = "Bearer"
    };

    return Results.Ok(response);
})
.WithName("RefreshToken");

// Logout endpoint
app.MapPost("/api/auth/logout", async (LogoutRequest request, JwtTokenService jwtService) =>
{
    // Revoke the refresh token
    await jwtService.RevokeRefreshTokenAsync(request.RefreshToken);

    return Results.Ok(new { message = "Logged out successfully" });
})
.WithName("Logout");

// Get Current User endpoint (requires authentication)
app.MapGet("/api/auth/user", async (HttpContext httpContext, ApplicationDbContext db) =>
{
    // Get user ID from JWT claims (using "sub" claim)
    var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
    if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
    {
        return Results.Unauthorized();
    }

    // Find user in database
    var user = await db.Users.FindAsync(userId);
    if (user == null)
    {
        return Results.NotFound(new { error = "User not found" });
    }

    // Return user response
    var userResponse = new UserResponse
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        DisplayName = user.DisplayName,
        AvatarUrl = user.AvatarUrl,
        CreatedAt = user.CreatedAt,
        TotalGamesPlayed = user.TotalGamesPlayed,
        TotalWins = user.TotalWins,
        TotalPrizeWon = user.TotalPrizeWon
    };

    return Results.Ok(userResponse);
})
.RequireAuthorization()
.WithName("GetCurrentUser");

// User Registration endpoint
app.MapPost("/api/users/register", async (RegisterUserRequest request, ApplicationDbContext db) =>
{
    // Check if username already exists
    if (await db.Users.AnyAsync(u => u.Username == request.Username))
    {
        return Results.BadRequest(new { error = "Username already exists" });
    }

    // Check if email already exists
    if (await db.Users.AnyAsync(u => u.Email == request.Email))
    {
        return Results.BadRequest(new { error = "Email already exists" });
    }

    // Hash password
    var passwordHash = PasswordHasher.HashPassword(request.Password);

    // Create user
    var user = new User
    {
        Username = request.Username,
        Email = request.Email,
        PasswordHash = passwordHash,
        DisplayName = request.DisplayName,
        CreatedAt = DateTime.UtcNow
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    // Return user response
    var userResponse = new UserResponse
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        DisplayName = user.DisplayName,
        AvatarUrl = user.AvatarUrl,
        CreatedAt = user.CreatedAt,
        TotalGamesPlayed = user.TotalGamesPlayed,
        TotalWins = user.TotalWins,
        TotalPrizeWon = user.TotalPrizeWon
    };

    return Results.Created($"/api/users/{user.Id}", userResponse);
})
.WithName("RegisterUser");

// List Games endpoint (public)
app.MapGet("/api/games", async (
    ApplicationDbContext db,
    int? status,
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
app.MapPost("/api/games", async (CreateGameRequest request, HttpContext httpContext, ApplicationDbContext db) =>
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
app.MapPost("/api/games/{id:guid}/join", async (Guid id, HttpContext httpContext, ApplicationDbContext db) =>
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
    await GameEventExtensions.BroadcastPlayerJoinedAsync(
        game.Id.ToString(),
        user.Id.ToString(),
        user.Username,
        user.DisplayName,
        playerSession.Position);

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
app.MapPost("/api/games/{id:guid}/leave", async (Guid id, HttpContext httpContext, ApplicationDbContext db) =>
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

    // If this is the game creator and there are other players, transfer ownership
    if (game.CreatedByUserId == userId && game.PlayerSessions.Count > 1)
    {
        // Transfer ownership to the next player (by position)
        var nextPlayer = game.PlayerSessions
            .Where(ps => ps.UserId != userId)
            .OrderBy(ps => ps.Position)
            .First();
        
        game.CreatedByUserId = nextPlayer.UserId;
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
        await GameEventExtensions.BroadcastPlayerLeftAsync(
            game.Id.ToString(),
            userId.ToString(),
            leavingPosition);
    }

    return Results.Ok(new { message = "Left game successfully" });
})
.RequireAuthorization()
.WithName("LeaveGame");

// Cancel Game endpoint (requires authentication, creator only)
app.MapDelete("/api/games/{id:guid}", async (Guid id, HttpContext httpContext, ApplicationDbContext db) =>
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

    return Results.Ok(new { message = "Game cancelled successfully" });
})
.RequireAuthorization()
.WithName("CancelGame");

// Start Game endpoint (requires authentication, creator only)
app.MapPost("/api/games/{id:guid}/start", async (Guid id, HttpContext httpContext, ApplicationDbContext db) =>
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
    await GameEventExtensions.BroadcastGameStartedAsync(
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

// Select Trump endpoint (requires authentication, party player only)
app.MapPost("/api/games/{id:guid}/rounds/current/trump", async (Guid id, SelectTrumpRequest request, HttpContext httpContext, ApplicationDbContext db) =>
{
    // Get user ID from JWT claims
    var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
    if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
    {
        return Results.Unauthorized();
    }

    // Find game with current round
    var game = await db.Games
        .Include(g => g.Rounds.OrderByDescending(r => r.RoundNumber).Take(1))
        .FirstOrDefaultAsync(g => g.Id == id);

    if (game == null)
    {
        return Results.NotFound(new { error = "Game not found" });
    }

    // Get current round
    var currentRound = game.Rounds.FirstOrDefault();
    if (currentRound == null)
    {
        return Results.BadRequest(new { error = "No active round found" });
    }

    // Check if round is in TrumpSelection phase
    if (currentRound.Status != RoundStatus.TrumpSelection)
    {
        return Results.StatusCode(409); // Conflict - wrong phase
    }

    // Check if user is the party player
    if (currentRound.PartyPlayerUserId != userId)
    {
        return Results.StatusCode(403); // Forbidden - only party player can select trump
    }

    // Validate trump suit selection rules
    if (request.SelectedBeforeDealing && request.TrumpSuit != TrumpSuit.Hearts)
    {
        return Results.BadRequest(new { error = "Only Hearts can be selected before dealing" });
    }

    // Calculate trick value based on trump suit and timing
    int trickValue;
    if (request.SelectedBeforeDealing)
    {
        // Selected before dealing (blind trump) - all values doubled
        trickValue = request.TrumpSuit switch
        {
            TrumpSuit.Hearts => 4,
            TrumpSuit.Diamonds => 2,
            TrumpSuit.Clubs => 2,
            TrumpSuit.Spades => 2,
            _ => 1
        };
    }
    else
    {
        // Selected after receiving 2 cards - normal values
        trickValue = request.TrumpSuit switch
        {
            TrumpSuit.Hearts => 2,
            TrumpSuit.Diamonds => 1,
            TrumpSuit.Clubs => 1,
            TrumpSuit.Spades => 1,
            _ => 1
        };
    }

    // Update round with trump selection
    currentRound.TrumpSuit = request.TrumpSuit;
    currentRound.TrumpSelectedBeforeDealing = request.SelectedBeforeDealing;
    currentRound.TrickValue = trickValue;
    
    // Move to next phase based on trump selection timing
    if (request.SelectedBeforeDealing)
    {
        // If trump was selected before dealing, move to Dealing phase first
        currentRound.Status = RoundStatus.Dealing;
    }
    else
    {
        // If trump was selected after 2 cards, move to PlayerDecisions phase
        currentRound.Status = RoundStatus.PlayerDecisions;
    }

    await db.SaveChangesAsync();

    // Broadcast trump selected event
    await GameEventExtensions.BroadcastTrumpSelectedAsync(
        game.Id.ToString(),
        request.TrumpSuit.ToString(),
        request.SelectedBeforeDealing,
        currentRound.TrickValue);

    // Return response
    var response = new SelectTrumpResponse
    {
        RoundId = currentRound.Id,
        TrumpSuit = currentRound.TrumpSuit,
        TrumpSelectedBeforeDealing = currentRound.TrumpSelectedBeforeDealing,
        TrickValue = currentRound.TrickValue
    };

    return Results.Ok(response);
})
.RequireAuthorization()
.WithName("SelectTrump");

// Player Decision endpoint (requires authentication, PlayerDecisions phase)
app.MapPost("/api/games/{id:guid}/rounds/current/play-decision", async (Guid id, PlayDecisionRequest request, HttpContext httpContext, ApplicationDbContext db) =>
{
    // Get user ID from JWT claims
    var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
    if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
    {
        return Results.Unauthorized();
    }

    // Find game with current round and player sessions
    var game = await db.Games
        .Include(g => g.Rounds.OrderByDescending(r => r.RoundNumber).Take(1))
        .Include(g => g.PlayerSessions)
        .FirstOrDefaultAsync(g => g.Id == id);

    if (game == null)
    {
        return Results.NotFound(new { error = "Game not found" });
    }

    // Get current round
    var currentRound = game.Rounds.FirstOrDefault();
    if (currentRound == null)
    {
        return Results.BadRequest(new { error = "No active round found" });
    }

    // Check if round is in PlayerDecisions phase
    if (currentRound.Status != RoundStatus.PlayerDecisions)
    {
        return Results.StatusCode(409); // Conflict - wrong phase
    }

    // Find player session for current user
    var playerSession = game.PlayerSessions.FirstOrDefault(ps => ps.UserId == userId);
    if (playerSession == null)
    {
        return Results.NotFound(new { error = "Player not in this game" });
    }

    // Validate decision rules
    // Rule 1: Party player must always play
    if (currentRound.PartyPlayerUserId == userId && !request.WillPlay)
    {
        return Results.BadRequest(new { error = "Party player cannot opt out" });
    }

    // Rule 2: Dealer must always play
    if (currentRound.DealerUserId == userId && !request.WillPlay)
    {
        return Results.BadRequest(new { error = "Dealer cannot opt out" });
    }

    // Rule 3: If trump is Clubs, all players must play
    if (currentRound.TrumpSuit == TrumpSuit.Clubs && !request.WillPlay)
    {
        return Results.BadRequest(new { error = "Clubs trump forces all players to play" });
    }

    // Rule 4: Cannot sit out more than 2 consecutive rounds
    if (!request.WillPlay && playerSession.ConsecutiveRoundsOut >= 2)
    {
        return Results.BadRequest(new { error = "Cannot sit out more than 2 consecutive rounds" });
    }

    // Rule 5: Players with 5 points or less must play (from game rules)
    if (!request.WillPlay && playerSession.CurrentPoints <= 5)
    {
        return Results.BadRequest(new { error = "Players with 5 points or less must play" });
    }

    // Update consecutive rounds out counter
    if (!request.WillPlay)
    {
        playerSession.ConsecutiveRoundsOut++;
    }
    else
    {
        // Reset counter if player decides to play
        playerSession.ConsecutiveRoundsOut = 0;
    }

    // Create or update Hand to track who's playing
    var existingHand = await db.Hands
        .FirstOrDefaultAsync(h => h.RoundId == currentRound.Id && h.PlayerSessionId == playerSession.Id);

    if (request.WillPlay)
    {
        if (existingHand == null)
        {
            // Create hand for player (cards will be dealt later)
            var hand = new Hand
            {
                RoundId = currentRound.Id,
                PlayerSessionId = playerSession.Id,
                CardsJson = "[]", // Empty for now, will be populated during dealing
                InitialCardsJson = "[]"
            };
            db.Hands.Add(hand);
        }
    }
    else
    {
        // Player opts out - remove hand if it exists
        if (existingHand != null)
        {
            db.Hands.Remove(existingHand);
        }
    }

    await db.SaveChangesAsync();

    // Return response
    var response = new PlayDecisionResponse
    {
        RoundId = currentRound.Id,
        PlayerSessionId = playerSession.Id,
        WillPlay = request.WillPlay,
        ConsecutiveRoundsOut = playerSession.ConsecutiveRoundsOut
    };

    return Results.Ok(response);
})
.RequireAuthorization()
.WithName("PlayDecision");

// Deal Cards endpoint (requires authentication, handles automatic dealing based on phase)
app.MapPost("/api/games/{id:guid}/rounds/current/deal-cards", async (Guid id, HttpContext httpContext, ApplicationDbContext db) =>
{
    // Get user ID from JWT claims
    var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
    if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
    {
        return Results.Unauthorized();
    }

    // Find game with current round, player sessions, and hands
    var game = await db.Games
        .Include(g => g.Rounds.OrderByDescending(r => r.RoundNumber).Take(1))
        .Include(g => g.PlayerSessions)
        .FirstOrDefaultAsync(g => g.Id == id);

    if (game == null)
    {
        return Results.NotFound(new { error = "Game not found" });
    }

    // Get current round
    var currentRound = game.Rounds.FirstOrDefault();
    if (currentRound == null)
    {
        return Results.BadRequest(new { error = "No active round found" });
    }

    // Load hands for current round
    var hands = await db.Hands
        .Where(h => h.RoundId == currentRound.Id)
        .Include(h => h.PlayerSession)
        .ToListAsync();

    // Determine dealing logic based on round status
    if (currentRound.Status == RoundStatus.Dealing)
    {
        // Trump selected before dealing - deal 5 cards to all active players
        var activePlayers = game.PlayerSessions.Where(ps => ps.IsActive).OrderBy(ps => ps.Position).ToList();
        
        if (activePlayers.Count == 0)
        {
            return Results.BadRequest(new { error = "No active players in game" });
        }

        // Create deck and shuffle
        var deck = CardDealingService.CreateDeck();
        CardDealingService.ShuffleDeck(deck);

        // Deal 5 cards to each active player
        var dealerPosition = game.CurrentDealerPosition ?? 0;
        var playerPositions = activePlayers.Select(p => p.Position).ToList();
        var dealtCards = CardDealingService.DealCards(deck, playerPositions, dealerPosition, 5);

        // Create hands for all active players
        foreach (var player in activePlayers)
        {
            var existingHand = hands.FirstOrDefault(h => h.PlayerSessionId == player.Id);
            var cards = dealtCards[player.Position];
            var cardsJson = System.Text.Json.JsonSerializer.Serialize(cards);

            if (existingHand == null)
            {
                var hand = new Hand
                {
                    RoundId = currentRound.Id,
                    PlayerSessionId = player.Id,
                    CardsJson = cardsJson,
                    InitialCardsJson = cardsJson
                };
                db.Hands.Add(hand);
            }
            else
            {
                existingHand.CardsJson = cardsJson;
                existingHand.InitialCardsJson = cardsJson;
            }
        }

        // Move to CardExchange phase
        currentRound.Status = RoundStatus.CardExchange;
        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = "Dealt 5 cards to all players",
            roundId = currentRound.Id,
            status = currentRound.Status.ToString(),
            playersDealt = activePlayers.Count
        });
    }
    else if (currentRound.Status == RoundStatus.PlayerDecisions)
    {
        // Check if all active players have made their decisions
        var activePlayers = game.PlayerSessions.Where(ps => ps.IsActive).ToList();
        var playersMadeDecisions = hands.Count;
        
        // Get dealer and party player (they always play)
        var dealerSession = game.PlayerSessions.FirstOrDefault(ps => ps.UserId == currentRound.DealerUserId);
        var partyPlayerSession = game.PlayerSessions.FirstOrDefault(ps => ps.UserId == currentRound.PartyPlayerUserId);
        
        // Expected number of hands = players who opted in + dealer + party player (if not already counted)
        var expectedHands = activePlayers.Count; // For simplicity, assuming all decisions have been made
        
        // For a more robust check, we'd need to track decision state per player
        // For now, allow dealing if at least dealer and party player have hands
        if (hands.Count < 2)
        {
            return Results.BadRequest(new { error = "Not all players have made their decisions yet" });
        }

        // Trump selected after 2 cards - deal remaining 3 cards to players with hands
        // Note: In real implementation, 2 cards should have been dealt already
        // For now, we'll deal 3 more cards to complete the 5-card hands
        
        var deck = CardDealingService.CreateDeck();
        CardDealingService.ShuffleDeck(deck);

        // Remove already-dealt cards from deck (simulate)
        // In a real implementation, we'd track which cards have been dealt
        // For simplicity, we'll just deal 3 new cards to each player with a hand
        
        var dealerPosition = game.CurrentDealerPosition ?? 0;
        var playingPlayerPositions = hands.Where(h => h.PlayerSession != null).Select(h => h.PlayerSession!.Position).ToList();
        var dealtCards = CardDealingService.DealCards(deck, playingPlayerPositions, dealerPosition, 3);

        // Add 3 cards to each existing hand
        foreach (var hand in hands)
        {
            if (hand.PlayerSession == null) continue;
            
            var currentCards = System.Text.Json.JsonSerializer.Deserialize<List<Card>>(hand.CardsJson) ?? new List<Card>();
            var newCards = dealtCards[hand.PlayerSession.Position];
            currentCards.AddRange(newCards);
            hand.CardsJson = System.Text.Json.JsonSerializer.Serialize(currentCards);
            
            // Update initial cards if not set
            if (string.IsNullOrEmpty(hand.InitialCardsJson) || hand.InitialCardsJson == "[]")
            {
                hand.InitialCardsJson = hand.CardsJson;
            }
        }

        // Move to CardExchange phase
        currentRound.Status = RoundStatus.CardExchange;
        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = "Dealt 3 additional cards to players",
            roundId = currentRound.Id,
            status = currentRound.Status.ToString(),
            playersDealt = hands.Count
        });
    }
    else
    {
        return Results.StatusCode(409); // Conflict - wrong phase for dealing
    }
})
.RequireAuthorization()
.WithName("DealCards");

// Exchange Cards endpoint (requires authentication)
app.MapPost("/api/games/{id:guid}/rounds/current/exchange-cards", async (Guid id, ExchangeCardsRequest request, HttpContext httpContext, ApplicationDbContext db) =>
{
    // Get user ID from JWT claims
    var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
    if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
    {
        return Results.Unauthorized();
    }

    // Validate request
    if (request.CardsToExchange.Count > 3)
    {
        return Results.BadRequest(new { error = "Cannot exchange more than 3 cards" });
    }

    // Validate all cards are valid
    foreach (var card in request.CardsToExchange)
    {
        if (!card.IsValid())
        {
            return Results.BadRequest(new { error = $"Invalid card: {card}" });
        }
    }

    // Find game with player sessions
    var game = await db.Games
        .Include(g => g.PlayerSessions)
        .FirstOrDefaultAsync(g => g.Id == id);

    if (game == null)
    {
        return Results.NotFound(new { error = "Game not found" });
    }

    // Get current round with hands
    var currentRound = await db.Rounds
        .Include(r => r.Hands)
        .ThenInclude(h => h.PlayerSession)
        .Where(r => r.GameId == id)
        .OrderByDescending(r => r.RoundNumber)
        .FirstOrDefaultAsync();
    if (currentRound == null)
    {
        return Results.NotFound(new { error = "No active round found" });
    }

    // Verify round is in CardExchange phase
    if (currentRound.Status != RoundStatus.CardExchange)
    {
        return Results.StatusCode(409); // Conflict - wrong phase
    }

    // Find player's session
    var playerSession = game.PlayerSessions.FirstOrDefault(ps => ps.UserId == userId);
    if (playerSession == null)
    {
        return Results.NotFound(new { error = "Player not in this game" });
    }

    // Find player's hand
    var hand = currentRound.Hands.FirstOrDefault(h => h.PlayerSessionId == playerSession.Id);
    if (hand == null)
    {
        return Results.BadRequest(new { error = "Player is not playing this round" });
    }

    // Get current cards
    var currentCards = hand.Cards;

    // Validate player has all cards they want to exchange
    foreach (var cardToExchange in request.CardsToExchange)
    {
        var hasCard = currentCards.Any(c => c.Suit == cardToExchange.Suit && c.Rank == cardToExchange.Rank);
        if (!hasCard)
        {
            return Results.BadRequest(new { error = $"You don't have the card: {cardToExchange}" });
        }
    }

    // Validate not trying to exchange Ace of trump
    var trumpSuit = currentRound.TrumpSuit.ToString();
    foreach (var cardToExchange in request.CardsToExchange)
    {
        if (cardToExchange.Rank == "Ace" && cardToExchange.Suit == trumpSuit)
        {
            return Results.BadRequest(new { error = "Cannot exchange the Ace of trump" });
        }
    }

    // If no cards to exchange, return current hand
    if (request.CardsToExchange.Count == 0)
    {
        return Results.Ok(new ExchangeCardsResponse
        {
            RoundId = currentRound.Id,
            PlayerSessionId = playerSession.Id,
            CardsExchanged = 0,
            NewHand = currentCards
        });
    }

    // Remove cards from hand
    foreach (var cardToExchange in request.CardsToExchange)
    {
        var cardToRemove = currentCards.First(c => c.Suit == cardToExchange.Suit && c.Rank == cardToExchange.Rank);
        currentCards.Remove(cardToRemove);
    }

    // Create new deck for drawing replacement cards (exclude cards still in play)
    var deck = CardDealingService.CreateDeck();
    
    // Remove all cards that are currently in any player's hand
    var allPlayersCards = currentRound.Hands
        .SelectMany(h => h.Cards)
        .ToList();
    
    foreach (var cardInPlay in allPlayersCards)
    {
        var cardToRemove = deck.FirstOrDefault(c => c.Suit == cardInPlay.Suit && c.Rank == cardInPlay.Rank);
        if (cardToRemove != null)
        {
            deck.Remove(cardToRemove);
        }
    }

    // Shuffle remaining deck
    CardDealingService.ShuffleDeck(deck);

    // Draw replacement cards
    var newCards = deck.Take(request.CardsToExchange.Count).ToList();
    currentCards.AddRange(newCards);

    // Update hand
    hand.CardsJson = System.Text.Json.JsonSerializer.Serialize(currentCards);
    await db.SaveChangesAsync();

    return Results.Ok(new ExchangeCardsResponse
    {
        RoundId = currentRound.Id,
        PlayerSessionId = playerSession.Id,
        CardsExchanged = request.CardsToExchange.Count,
        NewHand = currentCards
    });
})
.RequireAuthorization()
.WithName("ExchangeCards");

// Play Card endpoint (requires authentication)
app.MapPost("/api/games/{id:guid}/rounds/current/play-card", async (Guid id, PlayCardRequest request,
    HttpContext httpContext, ApplicationDbContext db, TrickTakingService trickService) =>
{
    // Get user ID from JWT claims
    var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
    if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
    {
        return Results.Unauthorized();
    }

    // Validate card
    if (!request.Card.IsValid())
    {
        return Results.BadRequest(new { error = "Invalid card" });
    }

    // Find game with current round, player sessions, hands, and tricks
    var game = await db.Games
        .Include(g => g.PlayerSessions)
            .ThenInclude(ps => ps.User)
        .FirstOrDefaultAsync(g => g.Id == id);

    if (game == null)
    {
        return Results.NotFound(new { error = "Game not found" });
    }

    // Get current round
    var currentRound = await db.Rounds
        .Include(r => r.Hands)
        .Include(r => r.Tricks)
        .OrderByDescending(r => r.RoundNumber)
        .FirstOrDefaultAsync(r => r.GameId == id);

    if (currentRound == null)
    {
        return Results.NotFound(new { error = "No active round found" });
    }

    // Validate round is in Playing or CardExchange phase
    if (currentRound.Status != RoundStatus.Playing && currentRound.Status != RoundStatus.CardExchange)
    {
        return Results.Conflict(new { error = $"Round is not in playing phase (current status: {currentRound.Status})" });
    }

    // Auto-transition from CardExchange to Playing if needed
    if (currentRound.Status == RoundStatus.CardExchange)
    {
        currentRound.Status = RoundStatus.Playing;
    }

    // Find player session
    var playerSession = game.PlayerSessions.FirstOrDefault(ps => ps.UserId == userId && ps.IsActive);
    if (playerSession == null)
    {
        return Results.NotFound(new { error = "Player not found in game" });
    }

    // Get player's hand
    var hand = currentRound.Hands.FirstOrDefault(h => h.PlayerSessionId == playerSession.Id);
    if (hand == null)
    {
        return Results.BadRequest(new { error = "Player is not playing this round" });
    }

    var playerHand = hand.Cards;

    // Get or create current trick
    var currentTrickNumber = currentRound.CurrentTrickNumber == 0 ? 1 : currentRound.CurrentTrickNumber;
    var currentTrick = currentRound.Tricks.FirstOrDefault(t => t.TrickNumber == currentTrickNumber);

    if (currentTrick == null)
    {
        // Create new trick - party player leads first trick
        var partyPlayerSession = game.PlayerSessions.First(ps => ps.UserId == currentRound.PartyPlayerUserId);
        currentTrick = new Trick
        {
            RoundId = currentRound.Id,
            TrickNumber = currentTrickNumber,
            LeadPlayerSessionId = currentTrickNumber == 1 ? partyPlayerSession.Id : currentRound.Tricks
                .Where(t => t.TrickNumber == currentTrickNumber - 1)
                .First().WinnerPlayerSessionId!.Value
        };
        db.Tricks.Add(currentTrick);
        currentRound.CurrentTrickNumber = currentTrickNumber;
    }

    var cardsPlayed = currentTrick.CardsPlayed;

    // Determine whose turn it is
    var activePlayers = game.PlayerSessions
        .Where(ps => currentRound.Hands.Any(h => h.PlayerSessionId == ps.Id))
        .OrderBy(ps => ps.Position)
        .ToList();

    var expectedPlayerSessionId = currentTrick.LeadPlayerSessionId;
    if (cardsPlayed.Count > 0)
    {
        var lastPlayerSessionId = cardsPlayed.Last().PlayerSessionId;
        var lastPlayerPosition = activePlayers.First(p => p.Id == lastPlayerSessionId).Position;
        var nextPosition = trickService.GetNextPlayerPosition(lastPlayerPosition, activePlayers.Select(p => p.Position).ToList());
        expectedPlayerSessionId = activePlayers.First(p => p.Position == nextPosition).Id;
    }

    if (expectedPlayerSessionId != playerSession.Id)
    {
        return Results.Json(new { error = "Not your turn" }, statusCode: 403);
    }

    // Get Ace of trump for validation
    var aceOfTrump = new Card { Rank = "Ace", Suit = currentRound.TrumpSuit.ToString() };

    // Validate card play
    var (isValid, errorMessage) = trickService.ValidateCardPlay(
        request.Card, 
        playerHand, 
        cardsPlayed, 
        currentRound.TrumpSuit, 
        aceOfTrump);

    if (!isValid)
    {
        return Results.BadRequest(new { error = errorMessage });
    }

    // Add card to trick
    cardsPlayed.Add(new CardPlayed 
    { 
        PlayerSessionId = playerSession.Id, 
        Card = request.Card 
    });
    currentTrick.CardsPlayed = cardsPlayed;

    // Remove card from player's hand
    playerHand.Remove(playerHand.First(c => c.Rank == request.Card.Rank && c.Suit == request.Card.Suit));
    hand.CardsJson = JsonSerializer.Serialize(playerHand);

    // Check if trick is completed (all active players have played)
    var trickCompleted = cardsPlayed.Count == activePlayers.Count;

    if (!trickCompleted)
    {
        // Trick not completed, return next player
        await db.SaveChangesAsync();
        
        // Broadcast card played event
        await GameEventExtensions.BroadcastCardPlayedAsync(
            game.Id.ToString(),
            playerSession.Position,
            request.Card.Rank,
            request.Card.Suit,
            currentTrickNumber);
        
        var nextPosition = trickService.GetNextPlayerPosition(playerSession.Position, activePlayers.Select(p => p.Position).ToList());

        return Results.Ok(new PlayCardResponse
        {
            RoundId = currentRound.Id,
            TrickNumber = currentTrickNumber,
            Card = request.Card,
            TrickCompleted = false,
            NextPlayerPosition = nextPosition,
            Winner = null,
            NextTrickLeader = null,
            RoundCompleted = false,
            Scores = null,
            GameCompleted = false
        });
    }

    // Trick completed - determine winner
    var winnerSessionId = trickService.DetermineTrickWinner(cardsPlayed, currentRound.TrumpSuit);
    currentTrick.WinnerPlayerSessionId = winnerSessionId;
    currentTrick.CompletedAt = DateTime.UtcNow;

    var winnerSession = activePlayers.First(p => p.Id == winnerSessionId);

    // Check if round is completed (all 5 tricks played)
    var roundCompleted = currentTrickNumber == 5;

    if (!roundCompleted)
    {
        // Round continues - prepare for next trick
        currentRound.CurrentTrickNumber++;
        await db.SaveChangesAsync();

        // Broadcast card played event
        await GameEventExtensions.BroadcastCardPlayedAsync(
            game.Id.ToString(),
            playerSession.Position,
            request.Card.Rank,
            request.Card.Suit,
            currentTrickNumber);

        // Broadcast trick completed event
        await GameEventExtensions.BroadcastTrickCompletedAsync(
            game.Id.ToString(),
            currentTrickNumber,
            winnerSession.Position,
            cardsPlayed.Select(cp => 
            {
                var ps = activePlayers.First(p => p.Id == cp.PlayerSessionId);
                return (Position: ps.Position, Rank: cp.Card.Rank, Suit: cp.Card.Suit);
            }).ToList());

        return Results.Ok(new PlayCardResponse
        {
            RoundId = currentRound.Id,
            TrickNumber = currentTrickNumber,
            Card = request.Card,
            TrickCompleted = true,
            NextPlayerPosition = null,
            Winner = new TrickWinner
            {
                Position = winnerSession.Position,
                UserId = winnerSession.UserId,
                DisplayName = winnerSession.User!.DisplayName
            },
            NextTrickLeader = winnerSession.Position,
            RoundCompleted = false,
            Scores = null,
            GameCompleted = false
        });
    }

    // Round completed - calculate scores
    currentRound.Status = RoundStatus.Completed;
    currentRound.CompletedAt = DateTime.UtcNow;

    var partyPlayerSessionId = game.PlayerSessions.First(ps => ps.UserId == currentRound.PartyPlayerUserId).Id;
    var roundScores = trickService.CalculateRoundScores(
        activePlayers,
        currentRound.Tricks.ToList(),
        currentRound.TrickValue,
        partyPlayerSessionId);

    // Update player points and create score history
    var scoreResponses = new List<RoundScore>();
    foreach (var (PlayerSessionId, TricksWon, PointsChange, Reason) in roundScores)
    {
        var player = activePlayers.First(p => p.Id == PlayerSessionId);
        player.CurrentPoints += PointsChange;

        var scoreHistory = new ScoreHistory
        {
            GameId = game.Id,
            PlayerSessionId = PlayerSessionId,
            RoundId = currentRound.Id,
            PointsChange = PointsChange,
            PointsAfter = player.CurrentPoints,
            Reason = Reason,
            CreatedAt = DateTime.UtcNow
        };
        db.ScoreHistories.Add(scoreHistory);

        scoreResponses.Add(new RoundScore
        {
            Position = player.Position,
            PointsChange = PointsChange,
            PointsAfter = player.CurrentPoints,
            TricksWon = TricksWon,
            Penalty = Reason == ScoreReason.NoTricksNormalPenalty || Reason == ScoreReason.NoTricksPartyPenalty,
            IsPartyPlayer = PlayerSessionId == partyPlayerSessionId
        });
    }

    // Check if game is completed
    var gameCompleted = trickService.IsGameComplete(game.PlayerSessions.ToList());
    if (gameCompleted)
    {
        game.Status = GameStatus.Completed;
        game.CompletedAt = DateTime.UtcNow;
    }

    await db.SaveChangesAsync();

    // Broadcast card played event
    await GameEventExtensions.BroadcastCardPlayedAsync(
        game.Id.ToString(),
        playerSession.Position,
        request.Card.Rank,
        request.Card.Suit,
        currentTrickNumber);

    // Broadcast trick completed event
    await GameEventExtensions.BroadcastTrickCompletedAsync(
        game.Id.ToString(),
        currentTrickNumber,
        winnerSession.Position,
        cardsPlayed.Select(cp => 
        {
            var ps = activePlayers.First(p => p.Id == cp.PlayerSessionId);
            return (Position: ps.Position, Rank: cp.Card.Rank, Suit: cp.Card.Suit);
        }).ToList());

    // Broadcast round completed event
    await GameEventExtensions.BroadcastRoundCompletedAsync(
        game.Id.ToString(),
        currentRound.Id.ToString(),
        currentRound.RoundNumber,
        scoreResponses.Select(s => (
            Position: s.Position,
            PointsChange: s.PointsChange,
            PointsAfter: s.PointsAfter,
            TricksWon: s.TricksWon,
            IsPenalty: s.Penalty,
            IsPartyPlayer: s.IsPartyPlayer
        )).ToList());

    // Broadcast game completed event if game is over
    if (gameCompleted)
    {
        var winner = game.PlayerSessions
            .OrderBy(ps => ps.CurrentPoints)
            .First();
        
        await GameEventExtensions.BroadcastGameCompletedAsync(
            game.Id.ToString(),
            winner.Position,
            winner.UserId.ToString(),
            game.CompletedAt!.Value,
            game.PlayerSessions.Select(ps => (
                Position: ps.Position,
                UserId: ps.UserId.ToString(),
                FinalPoints: ps.CurrentPoints,
                PrizeWon: Math.Max(0, 20 - ps.CurrentPoints) * 0.05
            )).ToList());
    }

    return Results.Ok(new PlayCardResponse
    {
        RoundId = currentRound.Id,
        TrickNumber = currentTrickNumber,
        Card = request.Card,
        TrickCompleted = true,
        NextPlayerPosition = null,
        Winner = new TrickWinner
        {
            Position = winnerSession.Position,
            UserId = winnerSession.UserId,
            DisplayName = winnerSession.User!.DisplayName
        },
        NextTrickLeader = null,
        RoundCompleted = true,
        Scores = scoreResponses,
        GameCompleted = gameCompleted
    });
})
.RequireAuthorization()
.WithName("PlayCard");

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

    // Find the game with all related entities
    var game = await db.Games
        .Include(g => g.CreatedBy)
        .Include(g => g.PlayerSessions)
            .ThenInclude(ps => ps.User)
        .Include(g => g.Rounds.OrderByDescending(r => r.RoundNumber).Take(1))
            .ThenInclude(r => r.Tricks)
        .Include(g => g.Rounds.OrderByDescending(r => r.RoundNumber).Take(1))
            .ThenInclude(r => r.Hands)
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
    if (game.Status == GameStatus.InProgress && game.Rounds.Any())
    {
        var currentRound = game.Rounds.OrderByDescending(r => r.RoundNumber).First();

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
        .Include(sh => sh.PlayerSession)
            .ThenInclude(ps => ps.User)
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

app.Run();

// Make Program class accessible to test projects
public partial class Program { }
