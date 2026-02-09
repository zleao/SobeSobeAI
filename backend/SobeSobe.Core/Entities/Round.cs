using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using SobeSobe.Core.Enums;
using SobeSobe.Core.ValueObjects;

namespace SobeSobe.Core.Entities;

public class Round
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required Guid GameId { get; set; }

    [Required]
    public int RoundNumber { get; set; }

    [Required]
    public required Guid DealerUserId { get; set; }

    [Required]
    public required Guid PartyPlayerUserId { get; set; }

    [Required]
    public TrumpSuit TrumpSuit { get; set; }

    public bool TrumpSelectedBeforeDealing { get; set; }

    [Required]
    [Range(1, 4)]
    public int TrickValue { get; set; }

    [Range(0, 5)]
    public int CurrentTrickNumber { get; set; } = 0;

    [Required]
    public RoundStatus Status { get; set; } = RoundStatus.Dealing;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    [Required]
    public string DeckJson { get; set; } = "[]";

    // Navigation properties
    [ForeignKey(nameof(GameId))]
    public Game? Game { get; set; }

    [ForeignKey(nameof(DealerUserId))]
    public User? Dealer { get; set; }

    [ForeignKey(nameof(PartyPlayerUserId))]
    public User? PartyPlayer { get; set; }

    public ICollection<Hand> Hands { get; set; } = new List<Hand>();
    public ICollection<Trick> Tricks { get; set; } = new List<Trick>();
    public ICollection<ScoreHistory> ScoreHistory { get; set; } = new List<ScoreHistory>();

    [NotMapped]
    public List<Card> Deck
    {
        get => JsonSerializer.Deserialize<List<Card>>(DeckJson) ?? new List<Card>();
        set => DeckJson = JsonSerializer.Serialize(value);
    }
}
