using SobeSobe.Core.Enums;

namespace SobeSobe.Api.DTOs;

public class GameResponse
{
    public Guid Id { get; set; }
    public Guid CreatedBy { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public GameStatus Status { get; set; }
    public int MaxPlayers { get; set; }
    public int CurrentPlayerCount { get; set; }
    public int CurrentRoundNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<PlayerSessionResponse> Players { get; set; } = new();
}

public class PlayerSessionResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int Position { get; set; }
    public int CurrentPoints { get; set; }
    public bool IsActive { get; set; }
    public int ConsecutiveRoundsOut { get; set; }
    public DateTime JoinedAt { get; set; }
}
