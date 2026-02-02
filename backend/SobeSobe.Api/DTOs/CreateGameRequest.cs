using System.ComponentModel.DataAnnotations;

namespace SobeSobe.Api.DTOs;

public class CreateGameRequest
{
    [Required]
    [Range(2, 5, ErrorMessage = "Game must have between 2 and 5 players")]
    public int MaxPlayers { get; set; }
}
