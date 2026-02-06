using Microsoft.EntityFrameworkCore;
using SobeSobe.Api.Services;
using SobeSobe.Core.Entities;
using SobeSobe.Infrastructure.Data;

namespace SobeSobe.Api.Seed;

public static class UserSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        var existingUsers = await db.Users
            .Where(u => u.Username == "zleao" || u.Username == "magu" || u.Email == "zleaopereira@gmail.com" || u.Email == "leaopereira@gmail.com")
            .Select(u => u.Username)
            .ToListAsync();

        if (existingUsers.Count == 2)
        {
            return;
        }

        if (!existingUsers.Contains("zleao"))
        {
            db.Users.Add(new User
            {
                Username = "zleao",
                Email = "zleaopereira@gmail.com",
                DisplayName = "José Pereira",
                PasswordHash = PasswordHasher.HashPassword("12345678"),
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!existingUsers.Contains("magu"))
        {
            db.Users.Add(new User
            {
                Username = "magu",
                Email = "leaopereira@gmail.com",
                DisplayName = "Marta Alves",
                PasswordHash = PasswordHasher.HashPassword("12345678"),
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}
