namespace SobeSobe.Api.DTOs;

public class StartGameResponse
{
    public required Guid GameId { get; set; }
    public required string Status { get; set; }
    public required DateTime StartedAt { get; set; }
    public required int CurrentRoundNumber { get; set; }
    public required int CurrentDealerPosition { get; set; }
}
