using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SobeSobe.Core.Entities;

public class PlayerSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required Guid GameId { get; set; }

    [Required]
    public required Guid UserId { get; set; }

    [Required]
    [Range(0, 4)]
    public int Position { get; set; }

    public int CurrentPoints { get; set; } = 20;

    public bool IsActive { get; set; } = true;

    [Range(0, 2)]
    public int ConsecutiveRoundsOut { get; set; } = 0;

    public int? LastDecisionRoundNumber { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LeftAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(GameId))]
    public Game? Game { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public ICollection<Hand> Hands { get; set; } = new List<Hand>();
    public ICollection<ScoreHistory> ScoreHistory { get; set; } = new List<ScoreHistory>();
}
