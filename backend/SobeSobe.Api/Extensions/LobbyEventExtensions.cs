using Google.Protobuf.WellKnownTypes;
using SobeSobe.Api.Protos;

namespace SobeSobe.Api.Extensions;

/// <summary>
/// Extension methods for broadcasting lobby events via gRPC.
/// </summary>
public static class LobbyEventExtensions
{
    /// <summary>
    /// Broadcasts a lobby list changed event to all subscribers.
    /// </summary>
    public static async Task BroadcastLobbyListChangedAsync(string? gameId = null)
    {
        var lobbyEvent = new LobbyEvent
        {
            Type = LobbyEventType.LobbyListChanged,
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
            GameId = gameId ?? string.Empty
        };

        await Services.LobbyEventsService.BroadcastLobbyEventAsync(lobbyEvent);
    }
}
