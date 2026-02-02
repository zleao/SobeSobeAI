using System.ComponentModel.DataAnnotations;

namespace SobeSobe.Api.DTOs;

public class LoginRequest
{
    [Required]
    public required string UsernameOrEmail { get; set; }

    [Required]
    public required string Password { get; set; }
}
