using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using SobeSobe.Core.ValueObjects;

namespace SobeSobe.Core.Entities;

public class Hand
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required Guid RoundId { get; set; }

    [Required]
    public required Guid PlayerSessionId { get; set; }

    [Required]
    [Column(TypeName = "TEXT")]
    public string CardsJson { get; set; } = "[]";

    [Required]
    [Column(TypeName = "TEXT")]
    public string InitialCardsJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(RoundId))]
    public Round? Round { get; set; }

    [ForeignKey(nameof(PlayerSessionId))]
    public PlayerSession? PlayerSession { get; set; }

    // Helper properties for working with cards
    [NotMapped]
    public List<Card> Cards
    {
        get => JsonSerializer.Deserialize<List<Card>>(CardsJson) ?? new List<Card>();
        set => CardsJson = JsonSerializer.Serialize(value);
    }

    [NotMapped]
    public List<Card> InitialCards
    {
        get => JsonSerializer.Deserialize<List<Card>>(InitialCardsJson) ?? new List<Card>();
        set => InitialCardsJson = JsonSerializer.Serialize(value);
    }
}
