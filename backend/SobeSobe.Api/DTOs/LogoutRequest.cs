using System.ComponentModel.DataAnnotations;

namespace SobeSobe.Api.DTOs;

public class LogoutRequest
{
    [Required]
    public required string RefreshToken { get; set; }
}
