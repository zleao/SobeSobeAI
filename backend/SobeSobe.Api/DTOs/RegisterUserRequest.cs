using System.ComponentModel.DataAnnotations;

namespace SobeSobe.Api.DTOs;

public class RegisterUserRequest
{
    [Required]
    [StringLength(20, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9_]{3,20}$", ErrorMessage = "Username must be 3-20 characters and contain only letters, numbers, and underscores")]
    public required string Username { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public required string Email { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public required string Password { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public required string DisplayName { get; set; }
}
