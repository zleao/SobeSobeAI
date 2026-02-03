using SobeSobe.Api.Services;
using SobeSobe.Core.Entities;
using SobeSobe.Core.Enums;
using SobeSobe.Core.ValueObjects;

namespace SobeSobe.Tests.Services;

public class TrickTakingServiceTests
{
    private readonly TrickTakingService _service;

    public TrickTakingServiceTests()
    {
        _service = new TrickTakingService();
    }

    #region DetermineTrickWinner Tests

    [Fact(DisplayName = "Highest trump card wins when multiple trumps played")]
    public void DetermineTrickWinner_MultipleTrumps_HighestTrumpWins()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();

        var cardsPlayed = new List<CardPlayed>
        {
            new CardPlayed { PlayerSessionId = player1, Card = new Card { Suit = "Hearts", Rank = "5" } },     // Trump but low
            new CardPlayed { PlayerSessionId = player2, Card = new Card { Suit = "Hearts", Rank = "Ace" } },   // Trump and highest (Ace = 10)
            new CardPlayed { PlayerSessionId = player3, Card = new Card { Suit = "Hearts", Rank = "7" } }      // Trump but medium (7 = 9)
        };

        var trumpSuit = TrumpSuit.Hearts;

        // Act
        var winnerId = _service.DetermineTrickWinner(cardsPlayed, trumpSuit);

        // Assert
        Assert.Equal(player2, winnerId);
    }

    [Fact(DisplayName = "Single trump card beats all non-trump cards")]
    public void DetermineTrickWinner_OneTrump_TrumpBeatsNonTrump()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();

        var cardsPlayed = new List<CardPlayed>
        {
            new CardPlayed { PlayerSessionId = player1, Card = new Card { Suit = "Spades", Rank = "Ace" } },   // Non-trump, even high value
            new CardPlayed { PlayerSessionId = player2, Card = new Card { Suit = "Hearts", Rank = "2" } },     // Trump but lowest rank
            new CardPlayed { PlayerSessionId = player3, Card = new Card { Suit = "Diamonds", Rank = "King" } } // Non-trump
        };

        var trumpSuit = TrumpSuit.Hearts;

        // Act
        var winnerId = _service.DetermineTrickWinner(cardsPlayed, trumpSuit);

        // Assert
        Assert.Equal(player2, winnerId); // Even lowest trump (2) beats non-trump Ace
    }

    [Fact(DisplayName = "Highest card of lead suit wins when no trumps played")]
    public void DetermineTrickWinner_NoTrumps_HighestLeadSuitWins()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();

        var cardsPlayed = new List<CardPlayed>
        {
            new CardPlayed { PlayerSessionId = player1, Card = new Card { Suit = "Spades", Rank = "King" } },   // Lead suit, high
            new CardPlayed { PlayerSessionId = player2, Card = new Card { Suit = "Diamonds", Rank = "Ace" } },  // Different suit, can't win
            new CardPlayed { PlayerSessionId = player3, Card = new Card { Suit = "Spades", Rank = "5" } }       // Lead suit but low
        };

        var trumpSuit = TrumpSuit.Hearts;

        // Act
        var winnerId = _service.DetermineTrickWinner(cardsPlayed, trumpSuit);

        // Assert
        Assert.Equal(player1, winnerId);
    }

    #endregion

    #region GetNextPlayerPosition Tests

    [Fact(DisplayName = "Counter-clockwise order: next player after position 0 is position 1")]
    public void GetNextPlayerPosition_FromZero_ReturnsOne()
    {
        // Arrange
        var playerPositions = new List<int> { 0, 1, 2, 3, 4 };
        var currentPosition = 0;

        // Act
        var nextPosition = _service.GetNextPlayerPosition(currentPosition, playerPositions);

        // Assert
        Assert.Equal(1, nextPosition);
    }

    [Fact(DisplayName = "Counter-clockwise order: wraps around from last to first")]
    public void GetNextPlayerPosition_FromLast_WrapsToFirst()
    {
        // Arrange
        var playerPositions = new List<int> { 0, 1, 2, 3, 4 };
        var currentPosition = 4;

        // Act
        var nextPosition = _service.GetNextPlayerPosition(currentPosition, playerPositions);

        // Assert
        Assert.Equal(0, nextPosition);
    }

    [Fact(DisplayName = "Counter-clockwise order: handles non-sequential positions")]
    public void GetNextPlayerPosition_NonSequential_WorksCorrectly()
    {
        // Arrange
        var playerPositions = new List<int> { 0, 2, 3 }; // Player at position 1 left game
        var currentPosition = 0;

        // Act
        var nextPosition = _service.GetNextPlayerPosition(currentPosition, playerPositions);

        // Assert
        Assert.Equal(2, nextPosition);
    }

    [Fact(DisplayName = "Counter-clockwise order: wraps correctly with non-sequential positions")]
    public void GetNextPlayerPosition_NonSequentialWrap_WorksCorrectly()
    {
        // Arrange
        var playerPositions = new List<int> { 0, 2, 3 }; // Player at position 1 left game
        var currentPosition = 3;

        // Act
        var nextPosition = _service.GetNextPlayerPosition(currentPosition, playerPositions);

        // Assert
        Assert.Equal(0, nextPosition); // Wraps to first position
    }

    #endregion

    #region CalculateRoundScores Tests

    [Fact(DisplayName = "Players who won tricks reduce points correctly")]
    public void CalculateRoundScores_TricksWon_PointsReduced()
    {
        // Arrange
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var player3Id = Guid.NewGuid();

        var playerSessions = new List<PlayerSession>
        {
            new PlayerSession { Id = player1Id, UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 0, CurrentPoints = 20, IsActive = true },
            new PlayerSession { Id = player2Id, UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 1, CurrentPoints = 15, IsActive = true },
            new PlayerSession { Id = player3Id, UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 2, CurrentPoints = 10, IsActive = true }
        };

        var roundId = Guid.NewGuid();
        var tricks = new List<Trick>
        {
            new Trick { Id = Guid.NewGuid(), RoundId = roundId, TrickNumber = 1, LeadPlayerSessionId = player1Id, WinnerPlayerSessionId = player1Id, CardsPlayedJson = "[]", CompletedAt = DateTime.UtcNow },
            new Trick { Id = Guid.NewGuid(), RoundId = roundId, TrickNumber = 2, LeadPlayerSessionId = player1Id, WinnerPlayerSessionId = player1Id, CardsPlayedJson = "[]", CompletedAt = DateTime.UtcNow },
            new Trick { Id = Guid.NewGuid(), RoundId = roundId, TrickNumber = 3, LeadPlayerSessionId = player1Id, WinnerPlayerSessionId = player2Id, CardsPlayedJson = "[]", CompletedAt = DateTime.UtcNow },
            new Trick { Id = Guid.NewGuid(), RoundId = roundId, TrickNumber = 4, LeadPlayerSessionId = player2Id, WinnerPlayerSessionId = player2Id, CardsPlayedJson = "[]", CompletedAt = DateTime.UtcNow },
            new Trick { Id = Guid.NewGuid(), RoundId = roundId, TrickNumber = 5, LeadPlayerSessionId = player2Id, WinnerPlayerSessionId = player3Id, CardsPlayedJson = "[]", CompletedAt = DateTime.UtcNow }
        };
        var trickValue = 2;
        var partyPlayerSessionId = player1Id;

        // Act
        var scores = _service.CalculateRoundScores(playerSessions, tricks, trickValue, partyPlayerSessionId);

        // Assert
        Assert.Equal(3, scores.Count);

        // Player 1 won 2 tricks: -4 points (2 * 2)
        var player1Score = scores.First(s => s.PlayerSessionId == player1Id);
        Assert.Equal(2, player1Score.TricksWon);
        Assert.Equal(-4, player1Score.PointsChange);
        Assert.Equal(ScoreReason.TricksWon, player1Score.Reason);

        // Player 2 won 2 tricks: -4 points (2 * 2)
        var player2Score = scores.First(s => s.PlayerSessionId == player2Id);
        Assert.Equal(2, player2Score.TricksWon);
        Assert.Equal(-4, player2Score.PointsChange);
        Assert.Equal(ScoreReason.TricksWon, player2Score.Reason);

        // Player 3 won 1 trick: -2 points (1 * 2)
        var player3Score = scores.First(s => s.PlayerSessionId == player3Id);
        Assert.Equal(1, player3Score.TricksWon);
        Assert.Equal(-2, player3Score.PointsChange);
        Assert.Equal(ScoreReason.TricksWon, player3Score.Reason);
    }

    [Fact(DisplayName = "Player with zero tricks receives normal penalty")]
    public void CalculateRoundScores_ZeroTricks_NormalPenalty()
    {
        // Arrange
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var partyPlayerId = Guid.NewGuid();

        var playerSessions = new List<PlayerSession>
        {
            new PlayerSession { Id = player1Id, UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 0, CurrentPoints = 20, IsActive = true },
            new PlayerSession { Id = player2Id, UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 1, CurrentPoints = 15, IsActive = true },
            new PlayerSession { Id = partyPlayerId, UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 2, CurrentPoints = 10, IsActive = true }
        };

        var roundId = Guid.NewGuid();
        var tricks = new List<Trick>
        {
            new Trick { Id = Guid.NewGuid(), RoundId = roundId, TrickNumber = 1, LeadPlayerSessionId = player1Id, WinnerPlayerSessionId = player1Id, CardsPlayedJson = "[]", CompletedAt = DateTime.UtcNow },
            new Trick { Id = Guid.NewGuid(), RoundId = roundId, TrickNumber = 2, LeadPlayerSessionId = player1Id, WinnerPlayerSessionId = player1Id, CardsPlayedJson = "[]", CompletedAt = DateTime.UtcNow },
            new Trick { Id = Guid.NewGuid(), RoundId = roundId, TrickNumber = 3, LeadPlayerSessionId = player1Id, WinnerPlayerSessionId = player1Id, CardsPlayedJson = "[]", CompletedAt = DateTime.UtcNow },
            new Trick { Id = Guid.NewGuid(), RoundId = roundId, TrickNumber = 4, LeadPlayerSessionId = partyPlayerId, WinnerPlayerSessionId = partyPlayerId, CardsPlayedJson = "[]", CompletedAt = DateTime.UtcNow },
            new Trick { Id = Guid.NewGuid(), RoundId = roundId, TrickNumber = 5, LeadPlayerSessionId = partyPlayerId, WinnerPlayerSessionId = partyPlayerId, CardsPlayedJson = "[]", CompletedAt = DateTime.UtcNow }
        };
        var trickValue = 2; // Penalty: 10 points
        var partyPlayerSessionId = partyPlayerId;

        // Act
        var scores = _service.CalculateRoundScores(playerSessions, tricks, trickValue, partyPlayerSessionId);

        // Assert
        var player2Score = scores.First(s => s.PlayerSessionId == player2Id);
        Assert.Equal(0, player2Score.TricksWon);
        Assert.Equal(10, player2Score.PointsChange); // Penalty adds points
        Assert.Equal(ScoreReason.NoTricksNormalPenalty, player2Score.Reason);
    }

    [Fact(DisplayName = "Party player with zero tricks receives double penalty")]
    public void CalculateRoundScores_PartyPlayerZeroTricks_DoublePenalty()
    {
        // Arrange
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var partyPlayerId = Guid.NewGuid();

        var playerSessions = new List<PlayerSession>
        {
            new PlayerSession { Id = player1Id, UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 0, CurrentPoints = 20, IsActive = true },
            new PlayerSession { Id = player2Id, UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 1, CurrentPoints = 15, IsActive = true },
            new PlayerSession { Id = partyPlayerId, UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 2, CurrentPoints = 10, IsActive = true }
        };

        var roundId = Guid.NewGuid();
        var tricks = new List<Trick>
        {
            new Trick { Id = Guid.NewGuid(), RoundId = roundId, TrickNumber = 1, LeadPlayerSessionId = player1Id, WinnerPlayerSessionId = player1Id, CardsPlayedJson = "[]", CompletedAt = DateTime.UtcNow },
            new Trick { Id = Guid.NewGuid(), RoundId = roundId, TrickNumber = 2, LeadPlayerSessionId = player1Id, WinnerPlayerSessionId = player1Id, CardsPlayedJson = "[]", CompletedAt = DateTime.UtcNow },
            new Trick { Id = Guid.NewGuid(), RoundId = roundId, TrickNumber = 3, LeadPlayerSessionId = player1Id, WinnerPlayerSessionId = player1Id, CardsPlayedJson = "[]", CompletedAt = DateTime.UtcNow },
            new Trick { Id = Guid.NewGuid(), RoundId = roundId, TrickNumber = 4, LeadPlayerSessionId = player2Id, WinnerPlayerSessionId = player2Id, CardsPlayedJson = "[]", CompletedAt = DateTime.UtcNow },
            new Trick { Id = Guid.NewGuid(), RoundId = roundId, TrickNumber = 5, LeadPlayerSessionId = player2Id, WinnerPlayerSessionId = player2Id, CardsPlayedJson = "[]", CompletedAt = DateTime.UtcNow }
        };
        var trickValue = 2; // Normal penalty: 10, double: 20
        var partyPlayerSessionId = partyPlayerId;

        // Act
        var scores = _service.CalculateRoundScores(playerSessions, tricks, trickValue, partyPlayerSessionId);

        // Assert
        var partyScore = scores.First(s => s.PlayerSessionId == partyPlayerId);
        Assert.Equal(0, partyScore.TricksWon);
        Assert.Equal(20, partyScore.PointsChange); // Double penalty (2 * 10)
        Assert.Equal(ScoreReason.NoTricksPartyPenalty, partyScore.Reason);
    }

    #endregion

    #region IsGameComplete Tests

    [Fact(DisplayName = "Game is complete when any player has 0 or fewer points")]
    public void IsGameComplete_PlayerAtZero_ReturnsTrue()
    {
        // Arrange
        var playerSessions = new List<PlayerSession>
        {
            new PlayerSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 0, CurrentPoints = 5, IsActive = true },
            new PlayerSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 1, CurrentPoints = 0, IsActive = true }, // Winner
            new PlayerSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 2, CurrentPoints = 10, IsActive = true }
        };

        // Act
        var isComplete = _service.IsGameComplete(playerSessions);

        // Assert
        Assert.True(isComplete);
    }

    [Fact(DisplayName = "Game is complete when any player has negative points")]
    public void IsGameComplete_PlayerNegative_ReturnsTrue()
    {
        // Arrange
        var playerSessions = new List<PlayerSession>
        {
            new PlayerSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 0, CurrentPoints = 5, IsActive = true },
            new PlayerSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 1, CurrentPoints = -3, IsActive = true }, // Winner
            new PlayerSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 2, CurrentPoints = 10, IsActive = true }
        };

        // Act
        var isComplete = _service.IsGameComplete(playerSessions);

        // Assert
        Assert.True(isComplete);
    }

    [Fact(DisplayName = "Game is not complete when all players have positive points")]
    public void IsGameComplete_AllPositive_ReturnsFalse()
    {
        // Arrange
        var playerSessions = new List<PlayerSession>
        {
            new PlayerSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 0, CurrentPoints = 5, IsActive = true },
            new PlayerSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 1, CurrentPoints = 3, IsActive = true },
            new PlayerSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), GameId = Guid.NewGuid(), Position = 2, CurrentPoints = 10, IsActive = true }
        };

        // Act
        var isComplete = _service.IsGameComplete(playerSessions);

        // Assert
        Assert.False(isComplete);
    }

    #endregion
}
