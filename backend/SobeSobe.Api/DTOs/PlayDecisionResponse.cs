namespace SobeSobe.Api.DTOs;

public class PlayDecisionResponse
{
    public Guid RoundId { get; set; }
    public Guid PlayerSessionId { get; set; }
    public bool WillPlay { get; set; }
    public int ConsecutiveRoundsOut { get; set; }
}
