using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SobeSobe.Infrastructure.Data;
using SobeSobe.Api.DTOs;
using SobeSobe.Api.Options;
using SobeSobe.Api.Services;
using SobeSobe.Core.Entities;
using SobeSobe.Core.Enums;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=sobesobe.db"));

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

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

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

    // Return join response
    var joinResponse = new
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
    db.PlayerSessions.Remove(playerSession);

    // If no players left, delete the game
    if (game.PlayerSessions.Count == 1) // Only this player left
    {
        db.Games.Remove(game);
    }

    await db.SaveChangesAsync();

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

app.Run();
