using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace SobeSobe.Core.Entities;

public class Trick
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required Guid RoundId { get; set; }

    [Required]
    [Range(1, 5)]
    public int TrickNumber { get; set; }

    [Required]
    public required Guid LeadPlayerSessionId { get; set; }

    public Guid? WinnerPlayerSessionId { get; set; }

    [Required]
    [Column(TypeName = "TEXT")]
    public string CardsPlayedJson { get; set; } = "[]";

    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(RoundId))]
    public Round? Round { get; set; }

    [ForeignKey(nameof(LeadPlayerSessionId))]
    public PlayerSession? LeadPlayer { get; set; }

    [ForeignKey(nameof(WinnerPlayerSessionId))]
    public PlayerSession? WinnerPlayer { get; set; }

    // Helper property for working with cards played
    [NotMapped]
    public List<CardPlayed> CardsPlayed
    {
        get => JsonSerializer.Deserialize<List<CardPlayed>>(CardsPlayedJson) ?? new List<CardPlayed>();
        set => CardsPlayedJson = JsonSerializer.Serialize(value);
    }
}

public class CardPlayed
{
    public required Guid PlayerSessionId { get; set; }
    public required ValueObjects.Card Card { get; set; }
}
