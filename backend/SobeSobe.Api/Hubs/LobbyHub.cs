using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SobeSobe.Infrastructure.Data;

namespace SobeSobe.Api.Hubs;

/// <summary>
/// SignalR hub for lobby-wide realtime events.
/// </summary>
[Authorize]
public sealed class LobbyHub : Hub
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<LobbyHub> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LobbyHub"/> class.
    /// </summary>
    public LobbyHub(ApplicationDbContext db, ILogger<LobbyHub> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Validates the user on connect.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId(Context.User);
        if (!userId.HasValue)
        {
            _logger.LogWarning("LobbyHub connection missing user id");
            Context.Abort();
            return;
        }

        var userExists = await _db.Users.AnyAsync(u => u.Id == userId.Value);
        if (!userExists)
        {
            _logger.LogWarning("LobbyHub connection user not found {UserId}", userId);
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Extracts the user id claim from the current principal.
    /// </summary>
    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(raw, out var userId) ? userId : null;
    }
}
