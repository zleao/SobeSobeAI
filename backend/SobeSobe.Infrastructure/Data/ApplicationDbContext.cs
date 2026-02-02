using Microsoft.EntityFrameworkCore;
using SobeSobe.Core.Entities;

namespace SobeSobe.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<PlayerSession> PlayerSessions => Set<PlayerSession>();
    public DbSet<Hand> Hands => Set<Hand>();
    public DbSet<Trick> Tricks => Set<Trick>();
    public DbSet<ScoreHistory> ScoreHistories => Set<ScoreHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User entity configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            
            entity.Property(e => e.Username).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.TotalPrizeWon).HasPrecision(10, 2);

            // User -> Games (as creator)
            entity.HasMany(u => u.CreatedGames)
                .WithOne(g => g.CreatedBy)
                .HasForeignKey(g => g.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // User -> PlayerSessions
            entity.HasMany(u => u.PlayerSessions)
                .WithOne(ps => ps.User)
                .HasForeignKey(ps => ps.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Game entity configuration
        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.MaxPlayers).IsRequired();

            // Game -> Rounds
            entity.HasMany(g => g.Rounds)
                .WithOne(r => r.Game)
                .HasForeignKey(r => r.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            // Game -> PlayerSessions
            entity.HasMany(g => g.PlayerSessions)
                .WithOne(ps => ps.Game)
                .HasForeignKey(ps => ps.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            // Game -> ScoreHistory
            entity.HasMany(g => g.ScoreHistory)
                .WithOne(sh => sh.Game)
                .HasForeignKey(sh => sh.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Round entity configuration
        modelBuilder.Entity<Round>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GameId, e.RoundNumber });
            entity.HasIndex(e => e.Status);

            entity.Property(e => e.RoundNumber).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.TrumpSuit).IsRequired();
            entity.Property(e => e.TrickValue).IsRequired();

            // Round -> Dealer (User)
            entity.HasOne(r => r.Dealer)
                .WithMany()
                .HasForeignKey(r => r.DealerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Round -> Party Player (User)
            entity.HasOne(r => r.PartyPlayer)
                .WithMany()
                .HasForeignKey(r => r.PartyPlayerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Round -> Hands
            entity.HasMany(r => r.Hands)
                .WithOne(h => h.Round)
                .HasForeignKey(h => h.RoundId)
                .OnDelete(DeleteBehavior.Cascade);

            // Round -> Tricks
            entity.HasMany(r => r.Tricks)
                .WithOne(t => t.Round)
                .HasForeignKey(t => t.RoundId)
                .OnDelete(DeleteBehavior.Cascade);

            // Round -> ScoreHistory
            entity.HasMany(r => r.ScoreHistory)
                .WithOne(sh => sh.Round)
                .HasForeignKey(sh => sh.RoundId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PlayerSession entity configuration
        modelBuilder.Entity<PlayerSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GameId, e.Position }).IsUnique();
            entity.HasIndex(e => new { e.GameId, e.UserId });

            entity.Property(e => e.Position).IsRequired();
            entity.Property(e => e.CurrentPoints).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.ConsecutiveRoundsOut).IsRequired();

            // PlayerSession -> Hands
            entity.HasMany(ps => ps.Hands)
                .WithOne(h => h.PlayerSession)
                .HasForeignKey(h => h.PlayerSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Hand entity configuration
        modelBuilder.Entity<Hand>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RoundId, e.PlayerSessionId }).IsUnique();

            entity.Property(e => e.CardsJson).HasColumnName("Cards").IsRequired();
            entity.Property(e => e.InitialCardsJson).HasColumnName("InitialCards").IsRequired();

            entity.Ignore(e => e.Cards);
            entity.Ignore(e => e.InitialCards);
        });

        // Trick entity configuration
        modelBuilder.Entity<Trick>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RoundId, e.TrickNumber }).IsUnique();

            entity.Property(e => e.TrickNumber).IsRequired();
            entity.Property(e => e.CardsPlayedJson).HasColumnName("CardsPlayed").IsRequired();

            entity.Ignore(e => e.CardsPlayed);

            // Trick -> LeadPlayer
            entity.HasOne(t => t.LeadPlayer)
                .WithMany()
                .HasForeignKey(t => t.LeadPlayerSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Trick -> WinnerPlayer (nullable)
            entity.HasOne(t => t.WinnerPlayer)
                .WithMany()
                .HasForeignKey(t => t.WinnerPlayerSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ScoreHistory entity configuration
        modelBuilder.Entity<ScoreHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.GameId);
            entity.HasIndex(e => e.PlayerSessionId);
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.PointsChange).IsRequired();
            entity.Property(e => e.PointsAfter).IsRequired();
            entity.Property(e => e.Reason).IsRequired();
        });
    }
}
