using System.ComponentModel.DataAnnotations;

namespace SobeSobe.Api.DTOs;

public class RefreshTokenRequest
{
    [Required]
    public required string RefreshToken { get; set; }
}
