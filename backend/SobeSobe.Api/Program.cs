using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SobeSobe.Infrastructure.Data;
using SobeSobe.Api.DTOs;
using SobeSobe.Api.Services;
using SobeSobe.Core.Entities;
using SobeSobe.Core.Enums;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=sobesobe.db"));

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
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured"))),
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
        return Results.BadRequest(new { error = "Invalid username/email or password" });
    }

    // Verify password
    if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
    {
        return Results.BadRequest(new { error = "Invalid username/email or password" });
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
.WithName("LoginUser")
.WithOpenApi();

// Token Refresh endpoint
app.MapPost("/api/auth/refresh", async (RefreshTokenRequest request, JwtTokenService jwtService) =>
{
    // Validate refresh token
    var refreshToken = await jwtService.ValidateRefreshTokenAsync(request.RefreshToken);
    
    if (refreshToken == null)
    {
        return Results.BadRequest(new { error = "Invalid or expired refresh token" });
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
.WithName("RefreshToken")
.WithOpenApi();

// Logout endpoint
app.MapPost("/api/auth/logout", async (LogoutRequest request, JwtTokenService jwtService) =>
{
    // Revoke the refresh token
    await jwtService.RevokeRefreshTokenAsync(request.RefreshToken);

    return Results.Ok(new { message = "Logged out successfully" });
})
.WithName("Logout")
.WithOpenApi();

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
.WithName("GetCurrentUser")
.WithOpenApi();

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
.WithName("RegisterUser")
.WithOpenApi();

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
            Id = g.CreatedBy.Id,
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
                UserId = ps.User.Id,
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
.WithName("ListGames")
.WithOpenApi();

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
.WithName("CreateGame")
.WithOpenApi();

app.Run();
