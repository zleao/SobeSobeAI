using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SobeSobe.Infrastructure.Data;

namespace SobeSobe.Api.Hubs;

/// <summary>
/// SignalR hub for game-specific realtime events.
/// </summary>
[Authorize]
public sealed class GameHub : Hub
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<GameHub> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameHub"/> class.
    /// </summary>
    public GameHub(ApplicationDbContext db, ILogger<GameHub> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Adds the connection to the game group after validating membership.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var gameId = httpContext?.Request.Query["gameId"].ToString();
        if (string.IsNullOrWhiteSpace(gameId) || !Guid.TryParse(gameId, out _))
        {
            _logger.LogWarning("GameHub connection missing or invalid gameId");
            Context.Abort();
            return;
        }

        var userId = GetUserId(Context.User);
        if (!userId.HasValue)
        {
            _logger.LogWarning("GameHub connection missing user id");
            Context.Abort();
            return;
        }

        var isPlayer = await _db.PlayerSessions
            .AnyAsync(ps => ps.GameId.ToString().ToLower() == gameId.ToLower() && ps.UserId == userId.Value);

        if (!isPlayer)
        {
            _logger.LogWarning("User {UserId} is not a player in game {GameId}", userId, gameId);
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Removes the connection from the game group when disconnected.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var httpContext = Context.GetHttpContext();
        var gameId = httpContext?.Request.Query["gameId"].ToString();
        if (!string.IsNullOrWhiteSpace(gameId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId);
        }

        await base.OnDisconnectedAsync(exception);
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
