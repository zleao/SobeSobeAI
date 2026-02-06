namespace SobeSobe.Api.Services.Realtime;

/// <summary>
/// Broadcasts lobby events to connected realtime clients.
/// </summary>
public interface ILobbyEventBroadcaster
{
    /// <summary>
    /// Broadcasts a lobby list changed event.
    /// </summary>
    Task BroadcastLobbyListChangedAsync(string? gameId = null);
}
