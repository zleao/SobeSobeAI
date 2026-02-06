using Microsoft.AspNetCore.SignalR;
using SobeSobe.Api.Hubs;

namespace SobeSobe.Api.Services.Realtime;

/// <summary>
/// SignalR-based broadcaster for game events.
/// </summary>
public sealed class SignalRGameEventBroadcaster : IGameEventBroadcaster
{
    private readonly IHubContext<GameHub> _hubContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRGameEventBroadcaster"/> class.
    /// </summary>
    public SignalRGameEventBroadcaster(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public Task BroadcastPlayerJoinedAsync(string gameId, string userId, string username, string displayName, int position)
    {
        return _hubContext.Clients.Group(gameId).SendAsync(
            "GameEvent",
            new RealtimeEvent("PLAYER_JOINED", new { userId, username, displayName, position }));
    }

    /// <inheritdoc />
    public Task BroadcastPlayerLeftAsync(string gameId, string userId, int position)
    {
        return _hubContext.Clients.Group(gameId).SendAsync(
            "GameEvent",
            new RealtimeEvent("PLAYER_LEFT", new { userId, position }));
    }

    /// <inheritdoc />
    public Task BroadcastGameStartedAsync(string gameId, DateTime startedAt, int dealerPosition,
        List<(string UserId, string Username, string DisplayName, int Position, int Points)> players)
    {
        var payload = new
        {
            startedAt,
            dealerPosition,
            players = players.Select(p => new
            {
                p.UserId,
                p.Username,
                p.DisplayName,
                p.Position,
                p.Points
            })
        };

        return _hubContext.Clients.Group(gameId).SendAsync(
            "GameEvent",
            new RealtimeEvent("GAME_STARTED", payload));
    }

    /// <inheritdoc />
    public Task BroadcastTrumpSelectedAsync(string gameId, string trumpSuit, bool selectedBeforeDealing, int trickValue)
    {
        return _hubContext.Clients.Group(gameId).SendAsync(
            "GameEvent",
            new RealtimeEvent("TRUMP_SELECTED", new { trumpSuit, selectedBeforeDealing, trickValue }));
    }

    /// <inheritdoc />
    public Task BroadcastCardPlayedAsync(string gameId, int position, string rank, string suit, int trickNumber)
    {
        return _hubContext.Clients.Group(gameId).SendAsync(
            "GameEvent",
            new RealtimeEvent("CARD_PLAYED", new { position, rank, suit, trickNumber }));
    }

    /// <inheritdoc />
    public Task BroadcastTrickCompletedAsync(string gameId, int trickNumber, int winnerPosition,
        List<(int Position, string Rank, string Suit)> cardsPlayed)
    {
        var payload = new
        {
            trickNumber,
            winnerPosition,
            cardsPlayed = cardsPlayed.Select(c => new { c.Position, c.Rank, c.Suit })
        };

        return _hubContext.Clients.Group(gameId).SendAsync(
            "GameEvent",
            new RealtimeEvent("TRICK_COMPLETED", payload));
    }

    /// <inheritdoc />
    public Task BroadcastRoundCompletedAsync(string gameId, string roundId, int roundNumber,
        List<(int Position, int PointsChange, int PointsAfter, int TricksWon, bool IsPenalty, bool IsPartyPlayer)> scores)
    {
        var payload = new
        {
            roundId,
            roundNumber,
            scores = scores.Select(s => new
            {
                s.Position,
                s.PointsChange,
                s.PointsAfter,
                s.TricksWon,
                s.IsPenalty,
                s.IsPartyPlayer
            })
        };

        return _hubContext.Clients.Group(gameId).SendAsync(
            "GameEvent",
            new RealtimeEvent("ROUND_COMPLETED", payload));
    }

    /// <inheritdoc />
    public Task BroadcastGameCompletedAsync(string gameId, int winnerPosition, string winnerUserId, DateTime completedAt,
        List<(int Position, string UserId, int FinalPoints, double PrizeWon)> finalScores)
    {
        var payload = new
        {
            winnerPosition,
            winnerUserId,
            completedAt,
            finalScores = finalScores.Select(s => new
            {
                s.Position,
                s.UserId,
                s.FinalPoints,
                s.PrizeWon
            })
        };

        return _hubContext.Clients.Group(gameId).SendAsync(
            "GameEvent",
            new RealtimeEvent("GAME_COMPLETED", payload));
    }

    /// <inheritdoc />
    public Task BroadcastGameAbandonedAsync(string gameId, string userId, string message)
    {
        return _hubContext.Clients.Group(gameId).SendAsync(
            "GameEvent",
            new RealtimeEvent("GAME_ABANDONED", new { userId, message }));
    }
}
