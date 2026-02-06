using Microsoft.AspNetCore.SignalR;
using SobeSobe.Api.Hubs;

namespace SobeSobe.Api.Services.Realtime;

/// <summary>
/// SignalR-based broadcaster for lobby events.
/// </summary>
public sealed class SignalRLobbyEventBroadcaster : ILobbyEventBroadcaster
{
    private readonly IHubContext<LobbyHub> _hubContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRLobbyEventBroadcaster"/> class.
    /// </summary>
    public SignalRLobbyEventBroadcaster(IHubContext<LobbyHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public Task BroadcastLobbyListChangedAsync(string? gameId = null)
    {
        var payload = new { gameId = gameId ?? string.Empty };
        return _hubContext.Clients.All.SendAsync(
            "LobbyEvent",
            new RealtimeEvent("LOBBY_LIST_CHANGED", payload));
    }
}
