namespace SobeSobe.Api.DTOs;

public class UserResponse
{
    public required Guid Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TotalGamesPlayed { get; set; }
    public int TotalWins { get; set; }
    public decimal TotalPrizeWon { get; set; }
}
