namespace SobeSobe.Api.Services.Realtime;

/// <summary>
/// Broadcasts game events to connected realtime clients.
/// </summary>
public interface IGameEventBroadcaster
{
    /// <summary>
    /// Broadcasts a player joined event to the game group.
    /// </summary>
    Task BroadcastPlayerJoinedAsync(string gameId, string userId, string username, string displayName, int position);

    /// <summary>
    /// Broadcasts a player left event to the game group.
    /// </summary>
    Task BroadcastPlayerLeftAsync(string gameId, string userId, int position);

    /// <summary>
    /// Broadcasts a game started event to the game group.
    /// </summary>
    Task BroadcastGameStartedAsync(string gameId, DateTime startedAt, int dealerPosition,
        List<(string UserId, string Username, string DisplayName, int Position, int Points)> players);

    /// <summary>
    /// Broadcasts a trump selected event to the game group.
    /// </summary>
    Task BroadcastTrumpSelectedAsync(string gameId, string trumpSuit, bool selectedBeforeDealing, int trickValue);

    /// <summary>
    /// Broadcasts a card played event to the game group.
    /// </summary>
    Task BroadcastCardPlayedAsync(string gameId, int position, string rank, string suit, int trickNumber);

    /// <summary>
    /// Broadcasts a trick completed event to the game group.
    /// </summary>
    Task BroadcastTrickCompletedAsync(string gameId, int trickNumber, int winnerPosition,
        List<(int Position, string Rank, string Suit)> cardsPlayed);

    /// <summary>
    /// Broadcasts a round completed event to the game group.
    /// </summary>
    Task BroadcastRoundCompletedAsync(string gameId, string roundId, int roundNumber,
        List<(int Position, int PointsChange, int PointsAfter, int TricksWon, bool IsPenalty, bool IsPartyPlayer)> scores);

    /// <summary>
    /// Broadcasts a game completed event to the game group.
    /// </summary>
    Task BroadcastGameCompletedAsync(string gameId, int winnerPosition, string winnerUserId, DateTime completedAt,
        List<(int Position, string UserId, int FinalPoints, double PrizeWon)> finalScores);

    /// <summary>
    /// Broadcasts a game abandoned event to the game group.
    /// </summary>
    Task BroadcastGameAbandonedAsync(string gameId, string userId, string message);
}
