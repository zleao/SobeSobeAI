using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SobeSobe.Core.Enums;

namespace SobeSobe.Core.Entities;

public class ScoreHistory
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required Guid GameId { get; set; }

    [Required]
    public required Guid PlayerSessionId { get; set; }

    public Guid? RoundId { get; set; }

    [Required]
    public int PointsChange { get; set; }

    [Required]
    public int PointsAfter { get; set; }

    [Required]
    public ScoreReason Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(GameId))]
    public Game? Game { get; set; }

    [ForeignKey(nameof(PlayerSessionId))]
    public PlayerSession? PlayerSession { get; set; }

    [ForeignKey(nameof(RoundId))]
    public Round? Round { get; set; }
}
