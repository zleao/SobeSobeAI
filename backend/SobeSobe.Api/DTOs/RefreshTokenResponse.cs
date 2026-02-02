namespace SobeSobe.Api.DTOs;

public class RefreshTokenResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public required string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; } = 900; // 15 minutes in seconds
}
