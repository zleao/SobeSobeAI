using Microsoft.EntityFrameworkCore;
using SobeSobe.Api.DTOs;
using SobeSobe.Api.Services;
using SobeSobe.Infrastructure.Data;

namespace SobeSobe.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
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

        return app;
    }
}
