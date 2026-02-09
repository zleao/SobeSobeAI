using SobeSobe.Core.ValueObjects;

namespace SobeSobe.Api.Services;

public class CardDealingService
{
    private static readonly Random _random = new();

    /// <summary>
    /// Creates a standard 40-card SobeSobe deck
    /// </summary>
    public static List<Card> CreateDeck()
    {
        var deck = new List<Card>();
        
        foreach (var suit in Card.ValidSuits)
        {
            foreach (var rank in Card.ValidRanks)
            {
                deck.Add(new Card { Suit = suit, Rank = rank });
            }
        }
        
        return deck;
    }

    /// <summary>
    /// Shuffles a deck using Fisher-Yates algorithm
    /// </summary>
    public static void ShuffleDeck(List<Card> deck)
    {
        int n = deck.Count;
        for (int i = n - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]); // Swap
        }
    }

    /// <summary>
    /// Deals cards to players in counter-clockwise order
    /// </summary>
    /// <param name="deck">The deck to deal from</param>
    /// <param name="playerPositions">Player positions in ascending order</param>
    /// <param name="dealerPosition">Position of the dealer</param>
    /// <param name="cardsPerPlayer">Number of cards to deal to each player</param>
    /// <returns>Dictionary mapping player position to their dealt cards</returns>
    public static Dictionary<int, List<Card>> DealCards(
        List<Card> deck, 
        List<int> playerPositions, 
        int dealerPosition, 
        int cardsPerPlayer)
    {
        var hands = new Dictionary<int, List<Card>>();
        
        // Initialize hands for each player
        foreach (var position in playerPositions)
        {
            hands[position] = new List<Card>();
        }

        // Sort player positions to start from party player (dealer + 1, counter-clockwise)
        // Party player is the first to receive cards
        var partyPlayerPosition = GetNextPosition(dealerPosition, playerPositions.Count);
        var dealOrder = GetCounterClockwiseOrder(playerPositions, partyPlayerPosition);

        // Deal cards round-robin in counter-clockwise order
        for (int cardNum = 0; cardNum < cardsPerPlayer; cardNum++)
        {
            foreach (var position in dealOrder)
            {
                if (deck.Count == 0)
                {
                    throw new InvalidOperationException("Not enough cards in deck");
                }

                var nextCard = deck[0];
                deck.RemoveAt(0);
                hands[position].Add(nextCard);
            }
        }

        return hands;
    }

    /// <summary>
    /// Gets the next position counter-clockwise
    /// </summary>
    private static int GetNextPosition(int currentPosition, int totalPositions)
    {
        return (currentPosition + 1) % totalPositions;
    }

    /// <summary>
    /// Orders positions counter-clockwise starting from a given position
    /// </summary>
    private static List<int> GetCounterClockwiseOrder(List<int> positions, int startPosition)
    {
        var ordered = new List<int>();
        var sortedPositions = positions.OrderBy(p => p).ToList();
        var totalPositions = sortedPositions.Max() + 1;

        // Find the starting position in the sorted list
        var currentPos = startPosition;
        var visitedCount = 0;
        
        while (visitedCount < positions.Count)
        {
            if (sortedPositions.Contains(currentPos))
            {
                ordered.Add(currentPos);
                visitedCount++;
            }
            currentPos = (currentPos + 1) % totalPositions;
        }

        return ordered;
    }
}
