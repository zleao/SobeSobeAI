using SobeSobe.Core.ValueObjects;

namespace SobeSobe.Api.DTOs;

public class ExchangeCardsResponse
{
    public required Guid RoundId { get; set; }
    public required Guid PlayerSessionId { get; set; }
    public required int CardsExchanged { get; set; }
    public required List<Card> NewHand { get; set; }
}
