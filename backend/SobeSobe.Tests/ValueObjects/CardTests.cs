using SobeSobe.Core.ValueObjects;

namespace SobeSobe.Tests.ValueObjects;

public class CardTests
{
    #region IsValid Tests

    [Fact(DisplayName = "Valid card with standard suit and rank returns true")]
    public void IsValid_StandardCard_ReturnsTrue()
    {
        // Arrange
        var card = new Card { Suit = "Hearts", Rank = "Ace" };

        // Act
        var isValid = card.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact(DisplayName = "Card with invalid suit returns false")]
    public void IsValid_InvalidSuit_ReturnsFalse()
    {
        // Arrange
        var card = new Card { Suit = "InvalidSuit", Rank = "Ace" };

        // Act
        var isValid = card.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact(DisplayName = "Card with invalid rank returns false")]
    public void IsValid_InvalidRank_ReturnsFalse()
    {
        // Arrange
        var card = new Card { Suit = "Hearts", Rank = "8" }; // 8s excluded from deck

        // Act
        var isValid = card.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Theory(DisplayName = "All valid suits are recognized")]
    [InlineData("Hearts")]
    [InlineData("Diamonds")]
    [InlineData("Clubs")]
    [InlineData("Spades")]
    public void IsValid_AllValidSuits_ReturnsTrue(string suit)
    {
        // Arrange
        var card = new Card { Suit = suit, Rank = "Ace" };

        // Act
        var isValid = card.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Theory(DisplayName = "All valid ranks are recognized")]
    [InlineData("Ace")]
    [InlineData("7")]
    [InlineData("King")]
    [InlineData("Queen")]
    [InlineData("Jack")]
    [InlineData("6")]
    [InlineData("5")]
    [InlineData("4")]
    [InlineData("3")]
    [InlineData("2")]
    public void IsValid_AllValidRanks_ReturnsTrue(string rank)
    {
        // Arrange
        var card = new Card { Suit = "Hearts", Rank = rank };

        // Act
        var isValid = card.IsValid();

        // Assert
        Assert.True(isValid);
    }

    #endregion

    #region GetRankValue Tests

    [Fact(DisplayName = "Ace has highest rank value of 10")]
    public void GetRankValue_Ace_Returns10()
    {
        // Arrange
        var card = new Card { Suit = "Hearts", Rank = "Ace" };

        // Act
        var value = card.GetRankValue();

        // Assert
        Assert.Equal(10, value);
    }

    [Fact(DisplayName = "7 has second highest rank value of 9")]
    public void GetRankValue_Seven_Returns9()
    {
        // Arrange
        var card = new Card { Suit = "Hearts", Rank = "7" };

        // Act
        var value = card.GetRankValue();

        // Assert
        Assert.Equal(9, value);
    }

    [Fact(DisplayName = "2 has lowest rank value of 1")]
    public void GetRankValue_Two_Returns1()
    {
        // Arrange
        var card = new Card { Suit = "Hearts", Rank = "2" };

        // Act
        var value = card.GetRankValue();

        // Assert
        Assert.Equal(1, value);
    }

    [Theory(DisplayName = "All rank values follow correct order")]
    [InlineData("Ace", 10)]
    [InlineData("7", 9)]
    [InlineData("King", 8)]
    [InlineData("Queen", 7)]
    [InlineData("Jack", 6)]
    [InlineData("6", 5)]
    [InlineData("5", 4)]
    [InlineData("4", 3)]
    [InlineData("3", 2)]
    [InlineData("2", 1)]
    public void GetRankValue_AllRanks_ReturnsCorrectValue(string rank, int expectedValue)
    {
        // Arrange
        var card = new Card { Suit = "Hearts", Rank = rank };

        // Act
        var value = card.GetRankValue();

        // Assert
        Assert.Equal(expectedValue, value);
    }

    [Fact(DisplayName = "Invalid rank returns 0")]
    public void GetRankValue_InvalidRank_Returns0()
    {
        // Arrange
        var card = new Card { Suit = "Hearts", Rank = "InvalidRank" };

        // Act
        var value = card.GetRankValue();

        // Assert
        Assert.Equal(0, value);
    }

    #endregion

    #region ToString Tests

    [Fact(DisplayName = "ToString returns formatted card string")]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var card = new Card { Suit = "Hearts", Rank = "Ace" };

        // Act
        var result = card.ToString();

        // Assert
        Assert.Equal("Ace of Hearts", result);
    }

    #endregion

    #region Equality Tests (Record Type)

    [Fact(DisplayName = "Cards with same suit and rank are equal")]
    public void Equals_SameCardValues_ReturnsTrue()
    {
        // Arrange
        var card1 = new Card { Suit = "Hearts", Rank = "Ace" };
        var card2 = new Card { Suit = "Hearts", Rank = "Ace" };

        // Act & Assert
        Assert.Equal(card1, card2);
        Assert.True(card1 == card2);
    }

    [Fact(DisplayName = "Cards with different suit are not equal")]
    public void Equals_DifferentSuit_ReturnsFalse()
    {
        // Arrange
        var card1 = new Card { Suit = "Hearts", Rank = "Ace" };
        var card2 = new Card { Suit = "Spades", Rank = "Ace" };

        // Act & Assert
        Assert.NotEqual(card1, card2);
        Assert.False(card1 == card2);
    }

    [Fact(DisplayName = "Cards with different rank are not equal")]
    public void Equals_DifferentRank_ReturnsFalse()
    {
        // Arrange
        var card1 = new Card { Suit = "Hearts", Rank = "Ace" };
        var card2 = new Card { Suit = "Hearts", Rank = "King" };

        // Act & Assert
        Assert.NotEqual(card1, card2);
        Assert.False(card1 == card2);
    }

    #endregion
}
