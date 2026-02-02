using SobeSobe.Core.Entities;
using SobeSobe.Core.Enums;
using SobeSobe.Core.ValueObjects;
using System.Text.Json;

namespace SobeSobe.Api.Services;

public class TrickTakingService
{
    /// <summary>
    /// Validates if a card play is legal according to game rules
    /// </summary>
    public (bool IsValid, string? ErrorMessage) ValidateCardPlay(Card cardToPlay, List<Card> playerHand, 
        List<CardPlayed> currentTrickCards, TrumpSuit trumpSuit, Card? aceOfTrump)
    {
        // Check if card is in player's hand
        if (!playerHand.Any(c => c.Rank == cardToPlay.Rank && c.Suit == cardToPlay.Suit))
        {
            return (false, "Card not in hand");
        }

        // If first card of trick, any card is valid
        if (currentTrickCards.Count == 0)
        {
            // Check mandatory Ace of trump rule when leading
            if (aceOfTrump != null && 
                playerHand.Any(c => c.Rank == aceOfTrump.Rank && c.Suit == aceOfTrump.Suit) &&
                (cardToPlay.Rank != aceOfTrump.Rank || cardToPlay.Suit != aceOfTrump.Suit))
            {
                // Ace of trump must be played when leading if player has it
                return (false, $"Must play Ace of {trumpSuit} when leading");
            }
            return (true, null);
        }

        var leadCard = currentTrickCards[0].Card;
        var leadSuit = leadCard.Suit;
        var trumpSuitString = trumpSuit.ToString();

        // Check if player has cards of lead suit
        var hasLeadSuit = playerHand.Any(c => c.Suit == leadSuit);

        if (hasLeadSuit)
        {
            // Must follow suit
            if (cardToPlay.Suit != leadSuit)
            {
                return (false, $"Must follow suit ({leadSuit})");
            }
            return (true, null);
        }

        // Cannot follow suit - must play trump if have any (cortar rule)
        var hasTrump = playerHand.Any(c => c.Suit == trumpSuitString);

        if (hasTrump)
        {
            // Check if trump was led
            if (leadSuit == trumpSuitString)
            {
                // Trump escalation: must play higher trump if able
                if (cardToPlay.Suit != trumpSuitString)
                {
                    return (false, $"Must play trump when trump is led");
                }

                var highestTrumpPlayed = currentTrickCards
                    .Where(cp => cp.Card.Suit == trumpSuitString)
                    .Max(cp => cp.Card.GetRankValue());

                if (cardToPlay.GetRankValue() <= highestTrumpPlayed)
                {
                    // Check if player has higher trump
                    var hasHigherTrump = playerHand.Any(c => 
                        c.Suit == trumpSuitString && c.GetRankValue() > highestTrumpPlayed);

                    if (hasHigherTrump)
                    {
                        return (false, "Must play higher trump card (escalation rule)");
                    }
                }

                return (true, null);
            }
            else
            {
                // Not following suit, must cut with trump
                if (cardToPlay.Suit != trumpSuitString)
                {
                    return (false, $"Must play trump (cortar) when cannot follow suit");
                }

                // Check mandatory Ace of trump when cutting
                if (aceOfTrump != null &&
                    playerHand.Any(c => c.Rank == aceOfTrump.Rank && c.Suit == aceOfTrump.Suit) &&
                    (cardToPlay.Rank != aceOfTrump.Rank || cardToPlay.Suit != aceOfTrump.Suit))
                {
                    return (false, $"Must play Ace of {trumpSuit} when cutting");
                }

                return (true, null);
            }
        }

        // No lead suit, no trump - can play any card
        return (true, null);
    }

    /// <summary>
    /// Determines the winner of a trick
    /// </summary>
    public Guid DetermineTrickWinner(List<CardPlayed> cardsPlayed, TrumpSuit trumpSuit)
    {
        var trumpSuitString = trumpSuit.ToString();
        var leadSuit = cardsPlayed[0].Card.Suit;

        // Check if any trump cards were played
        var trumpCards = cardsPlayed.Where(cp => cp.Card.Suit == trumpSuitString).ToList();

        if (trumpCards.Any())
        {
            // Highest trump wins
            var winner = trumpCards.MaxBy(cp => cp.Card.GetRankValue());
            return winner!.PlayerSessionId;
        }

        // No trump played, highest card of lead suit wins
        var leadSuitCards = cardsPlayed.Where(cp => cp.Card.Suit == leadSuit).ToList();
        var winnerCard = leadSuitCards.MaxBy(cp => cp.Card.GetRankValue());
        return winnerCard!.PlayerSessionId;
    }

    /// <summary>
    /// Gets the next player position in counter-clockwise order
    /// </summary>
    public int GetNextPlayerPosition(int currentPosition, List<int> activePlayers)
    {
        var orderedPlayers = activePlayers.OrderBy(p => p).ToList();
        var currentIndex = orderedPlayers.IndexOf(currentPosition);
        var nextIndex = (currentIndex + 1) % orderedPlayers.Count;
        return orderedPlayers[nextIndex];
    }

    /// <summary>
    /// Calculates scores for a completed round
    /// </summary>
    public List<(Guid PlayerSessionId, int TricksWon, int PointsChange, ScoreReason Reason)> CalculateRoundScores(
        List<PlayerSession> activePlayers, 
        List<Trick> completedTricks, 
        int trickValue, 
        Guid partyPlayerSessionId)
    {
        var scores = new List<(Guid PlayerSessionId, int TricksWon, int PointsChange, ScoreReason Reason)>();

        foreach (var player in activePlayers)
        {
            var tricksWon = completedTricks.Count(t => t.WinnerPlayerSessionId == player.Id);
            var isPartyPlayer = player.Id == partyPlayerSessionId;

            if (tricksWon == 0)
            {
                // Penalty for no tricks
                var penalty = trickValue switch
                {
                    1 => 5,
                    2 => 10,
                    4 => 20,
                    _ => 5
                };

                // Party player penalty is doubled
                if (isPartyPlayer)
                {
                    penalty *= 2;
                    scores.Add((player.Id, 0, penalty, ScoreReason.NoTricksPartyPenalty));
                }
                else
                {
                    scores.Add((player.Id, 0, penalty, ScoreReason.NoTricksNormalPenalty));
                }
            }
            else
            {
                // Normal scoring: reduce points by tricks won * trick value
                var pointsChange = -(tricksWon * trickValue);
                scores.Add((player.Id, tricksWon, pointsChange, ScoreReason.TricksWon));
            }
        }

        return scores;
    }

    /// <summary>
    /// Checks if the game is complete (any player at or below 0 points)
    /// </summary>
    public bool IsGameComplete(List<PlayerSession> players)
    {
        return players.Any(p => p.CurrentPoints <= 0);
    }
}
