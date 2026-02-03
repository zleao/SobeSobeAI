namespace SobeSobe.Api.DTOs;

public class JoinGameResponse
{
    public required Guid GameId { get; set; }
    public required PlayerSessionResponse PlayerSession { get; set; }
}
