using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SobeSobe.Core.Enums;

namespace SobeSobe.Core.Entities;

public class Game
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required Guid CreatedByUserId { get; set; }

    [Required]
    public GameStatus Status { get; set; } = GameStatus.Waiting;

    [Required]
    [Range(2, 5)]
    public int MaxPlayers { get; set; }

    [Range(0, 4)]
    public int? CurrentDealerPosition { get; set; }

    public int CurrentRoundNumber { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public Guid? WinnerUserId { get; set; }

    // Navigation properties
    [ForeignKey(nameof(CreatedByUserId))]
    public User? CreatedBy { get; set; }

    [ForeignKey(nameof(WinnerUserId))]
    public User? Winner { get; set; }

    public ICollection<PlayerSession> PlayerSessions { get; set; } = new List<PlayerSession>();
    public ICollection<Round> Rounds { get; set; } = new List<Round>();
    public ICollection<ScoreHistory> ScoreHistory { get; set; } = new List<ScoreHistory>();
}
