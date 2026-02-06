using Microsoft.EntityFrameworkCore;
using SobeSobe.Api.DTOs;
using SobeSobe.Api.Services;
using SobeSobe.Core.Entities;
using SobeSobe.Infrastructure.Data;

namespace SobeSobe.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
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

        return app;
    }
}
