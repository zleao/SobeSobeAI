using SobeSobe.Core.ValueObjects;

namespace SobeSobe.Api.DTOs;

public class PlayCardResponse
{
    public required Guid RoundId { get; set; }
    public required int TrickNumber { get; set; }
    public required Card Card { get; set; }
    public bool TrickCompleted { get; set; }
    public int? NextPlayerPosition { get; set; }
    public TrickWinner? Winner { get; set; }
    public int? NextTrickLeader { get; set; }
    public bool RoundCompleted { get; set; }
    public List<RoundScore>? Scores { get; set; }
    public bool GameCompleted { get; set; }
}

public class TrickWinner
{
    public required int Position { get; set; }
    public required Guid UserId { get; set; }
    public required string DisplayName { get; set; }
}

public class RoundScore
{
    public required int Position { get; set; }
    public required int PointsChange { get; set; }
    public required int PointsAfter { get; set; }
    public required int TricksWon { get; set; }
    public bool Penalty { get; set; }
    public bool IsPartyPlayer { get; set; }
}
