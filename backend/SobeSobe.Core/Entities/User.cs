using System.ComponentModel.DataAnnotations;

namespace SobeSobe.Core.Entities;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(20, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9_]{3,20}$")]
    public required string Username { get; set; }

    [Required]
    [StringLength(254)]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    [StringLength(256, MinimumLength = 60)]
    public required string PasswordHash { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public required string DisplayName { get; set; }

    [StringLength(500)]
    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public int TotalGamesPlayed { get; set; } = 0;

    public int TotalWins { get; set; } = 0;

    public int TotalPointsScored { get; set; } = 0;

    public decimal TotalPrizeWon { get; set; } = 0.00m;

    // Navigation properties
    public ICollection<Game> CreatedGames { get; set; } = new List<Game>();
    public ICollection<PlayerSession> PlayerSessions { get; set; } = new List<PlayerSession>();
}
