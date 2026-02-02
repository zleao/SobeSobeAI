using System.ComponentModel.DataAnnotations;
using SobeSobe.Core.ValueObjects;

namespace SobeSobe.Api.DTOs;

public class ExchangeCardsRequest
{
    [Required]
    [MaxLength(3)]
    public required List<Card> CardsToExchange { get; set; }
}
