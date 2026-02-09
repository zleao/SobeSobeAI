using SobeSobe.Core.Enums;
using SobeSobe.Core.ValueObjects;

namespace SobeSobe.Api.DTOs;

public class GameStateResponse
{
    public Guid Id { get; set; }
    public Guid CreatedBy { get; set; }
    public required string CreatedByUsername { get; set; }
    public GameStatus Status { get; set; }
    public int MaxPlayers { get; set; }
    public int CurrentPlayerCount { get; set; }
    public int? CurrentDealerIndex { get; set; }
    public int CurrentRoundNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<PlayerStateResponse> Players { get; set; } = [];
    public RoundStateResponse? CurrentRound { get; set; }
}

public class PlayerStateResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public int Position { get; set; }
    public int CurrentPoints { get; set; }
    public bool IsActive { get; set; }
    public int ConsecutiveRoundsOut { get; set; }
    public int? LastDecisionRoundNumber { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public List<Card>? Hand { get; set; } // Only populated for requesting player
}

public class RoundStateResponse
{
    public Guid Id { get; set; }
    public int RoundNumber { get; set; }
    public Guid DealerUserId { get; set; }
    public Guid PartyPlayerUserId { get; set; }
    public TrumpSuit? TrumpSuit { get; set; }
    public bool TrumpSelectedBeforeDealing { get; set; }
    public int TrickValue { get; set; }
    public int CurrentTrickNumber { get; set; }
    public RoundStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<TrickStateResponse> Tricks { get; set; } = [];
    public TrickStateResponse? CurrentTrick { get; set; }
}

public class TrickStateResponse
{
    public Guid Id { get; set; }
    public int TrickNumber { get; set; }
    public Guid LeadPlayerSessionId { get; set; }
    public Guid? WinnerPlayerSessionId { get; set; }
    public List<CardPlayedResponse> CardsPlayed { get; set; } = [];
    public DateTime? CompletedAt { get; set; }
}

public class CardPlayedResponse
{
    public Guid PlayerSessionId { get; set; }
    public int PlayerPosition { get; set; }
    public required Card Card { get; set; }
}
