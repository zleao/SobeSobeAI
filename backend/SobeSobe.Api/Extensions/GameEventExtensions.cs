using SobeSobe.Api.Protos;
using Google.Protobuf.WellKnownTypes;

namespace SobeSobe.Api.Extensions;

/// <summary>
/// Extension methods for broadcasting game events via gRPC
/// </summary>
public static class GameEventExtensions
{
    /// <summary>
    /// Broadcast a player joined event
    /// </summary>
    public static async Task BroadcastPlayerJoinedAsync(
        string gameId,
        string userId,
        string username,
        string displayName,
        int position)
    {
        var gameEvent = new GameEvent
        {
            GameId = gameId,
            Type = EventType.PlayerJoined,
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
            PlayerJoined = new PlayerJoinedEvent
            {
                UserId = userId,
                Username = username,
                DisplayName = displayName,
                Position = position
            }
        };

        await Services.GameEventsService.BroadcastGameEventAsync(gameId, gameEvent);
    }

    /// <summary>
    /// Broadcast a player left event
    /// </summary>
    public static async Task BroadcastPlayerLeftAsync(
        string gameId,
        string userId,
        int position)
    {
        var gameEvent = new GameEvent
        {
            GameId = gameId,
            Type = EventType.PlayerLeft,
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
            PlayerLeft = new PlayerLeftEvent
            {
                UserId = userId,
                Position = position
            }
        };

        await Services.GameEventsService.BroadcastGameEventAsync(gameId, gameEvent);
    }

    /// <summary>
    /// Broadcast a game started event
    /// </summary>
    public static async Task BroadcastGameStartedAsync(
        string gameId,
        DateTime startedAt,
        int dealerPosition,
        List<(string UserId, string Username, string DisplayName, int Position, int Points)> players)
    {
        var gameEvent = new GameEvent
        {
            GameId = gameId,
            Type = EventType.GameStarted,
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
            GameStarted = new GameStartedEvent
            {
                StartedAt = Timestamp.FromDateTime(startedAt.ToUniversalTime()),
                DealerPosition = dealerPosition
            }
        };

        gameEvent.GameStarted.Players.AddRange(players.Select(p => new PlayerInfo
        {
            UserId = p.UserId,
            Username = p.Username,
            DisplayName = p.DisplayName,
            Position = p.Position,
            CurrentPoints = p.Points
        }));

        await Services.GameEventsService.BroadcastGameEventAsync(gameId, gameEvent);
    }

    /// <summary>
    /// Broadcast a trump selected event
    /// </summary>
    public static async Task BroadcastTrumpSelectedAsync(
        string gameId,
        string trumpSuit,
        bool selectedBeforeDealing,
        int trickValue)
    {
        var gameEvent = new GameEvent
        {
            GameId = gameId,
            Type = EventType.TrumpSelected,
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
            TrumpSelected = new TrumpSelectedEvent
            {
                TrumpSuit = trumpSuit,
                SelectedBeforeDealing = selectedBeforeDealing,
                TrickValue = trickValue
            }
        };

        await Services.GameEventsService.BroadcastGameEventAsync(gameId, gameEvent);
    }

    /// <summary>
    /// Broadcast a card played event
    /// </summary>
    public static async Task BroadcastCardPlayedAsync(
        string gameId,
        int position,
        string rank,
        string suit,
        int trickNumber)
    {
        var gameEvent = new GameEvent
        {
            GameId = gameId,
            Type = EventType.CardPlayed,
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
            CardPlayed = new CardPlayedEvent
            {
                Position = position,
                Card = new Card { Rank = rank, Suit = suit },
                TrickNumber = trickNumber
            }
        };

        await Services.GameEventsService.BroadcastGameEventAsync(gameId, gameEvent);
    }

    /// <summary>
    /// Broadcast a trick completed event
    /// </summary>
    public static async Task BroadcastTrickCompletedAsync(
        string gameId,
        int trickNumber,
        int winnerPosition,
        List<(int Position, string Rank, string Suit)> cardsPlayed)
    {
        var gameEvent = new GameEvent
        {
            GameId = gameId,
            Type = EventType.TrickCompleted,
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
            TrickCompleted = new TrickCompletedEvent
            {
                TrickNumber = trickNumber,
                WinnerPosition = winnerPosition
            }
        };

        gameEvent.TrickCompleted.CardsPlayed.AddRange(cardsPlayed.Select(c => new CardPlay
        {
            Position = c.Position,
            Card = new Card { Rank = c.Rank, Suit = c.Suit }
        }));

        await Services.GameEventsService.BroadcastGameEventAsync(gameId, gameEvent);
    }

    /// <summary>
    /// Broadcast a round completed event
    /// </summary>
    public static async Task BroadcastRoundCompletedAsync(
        string gameId,
        string roundId,
        int roundNumber,
        List<(int Position, int PointsChange, int PointsAfter, int TricksWon, bool IsPenalty, bool IsPartyPlayer)> scores)
    {
        var gameEvent = new GameEvent
        {
            GameId = gameId,
            Type = EventType.RoundCompleted,
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
            RoundCompleted = new RoundCompletedEvent
            {
                RoundId = roundId,
                RoundNumber = roundNumber
            }
        };

        gameEvent.RoundCompleted.Scores.AddRange(scores.Select(s => new RoundScore
        {
            Position = s.Position,
            PointsChange = s.PointsChange,
            PointsAfter = s.PointsAfter,
            TricksWon = s.TricksWon,
            IsPenalty = s.IsPenalty,
            IsPartyPlayer = s.IsPartyPlayer
        }));

        await Services.GameEventsService.BroadcastGameEventAsync(gameId, gameEvent);
    }

    /// <summary>
    /// Broadcast a game completed event
    /// </summary>
    public static async Task BroadcastGameCompletedAsync(
        string gameId,
        int winnerPosition,
        string winnerUserId,
        DateTime completedAt,
        List<(int Position, string UserId, int FinalPoints, double PrizeWon)> finalScores)
    {
        var gameEvent = new GameEvent
        {
            GameId = gameId,
            Type = EventType.GameCompleted,
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
            GameCompleted = new GameCompletedEvent
            {
                GameId = gameId,
                WinnerPosition = winnerPosition,
                WinnerUserId = winnerUserId,
                CompletedAt = Timestamp.FromDateTime(completedAt.ToUniversalTime())
            }
        };

        gameEvent.GameCompleted.FinalScores.AddRange(finalScores.Select(s => new FinalScore
        {
            Position = s.Position,
            UserId = s.UserId,
            FinalPoints = s.FinalPoints,
            PrizeWon = s.PrizeWon
        }));

        await Services.GameEventsService.BroadcastGameEventAsync(gameId, gameEvent);
    }
}
