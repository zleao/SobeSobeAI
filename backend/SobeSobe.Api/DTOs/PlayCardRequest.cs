using SobeSobe.Core.ValueObjects;

namespace SobeSobe.Api.DTOs;

public class PlayCardRequest
{
    public required Card Card { get; set; }
}
