using SobeSobe.Core.Enums;

namespace SobeSobe.Api.DTOs;

public class ScoreHistoryResponse
{
    public List<ScoreEntry> Scores { get; set; } = [];
}

public class ScoreEntry
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid PlayerSessionId { get; set; }
    public int PlayerPosition { get; set; }
    public required string PlayerUsername { get; set; }
    public required string PlayerDisplayName { get; set; }
    public Guid? RoundId { get; set; }
    public int? RoundNumber { get; set; }
    public int PointsChange { get; set; }
    public int PointsAfter { get; set; }
    public ScoreReason Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}
