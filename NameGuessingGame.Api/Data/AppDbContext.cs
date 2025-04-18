using Microsoft.EntityFrameworkCore;
using NameGuessingGame.Api.Models;

namespace NameGuessingGame.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Room> Rooms { get; set; } = null!;
    public DbSet<GameRound> GameRounds { get; set; } = null!;
    public DbSet<PlayerNameAssignment> PlayerNameAssignments { get; set; } = null!;
    public DbSet<QuestionAnswer> QuestionAnswers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.OwnerId).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.MaxPlayers).IsRequired();

            entity.HasMany(r => r.Players)
                  .WithOne()
                  .HasForeignKey(u => u.RoomId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GameRound>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RoomId).IsRequired();
            entity.Property(e => e.RoundNumber).IsRequired();
            entity.Property(e => e.StartedAt).IsRequired();

            entity.HasOne(g => g.Room)
                  .WithMany(r => r.GameRounds)
                  .HasForeignKey(g => g.RoomId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerNameAssignment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GameRoundId).IsRequired();
            entity.Property(e => e.PlayerId).IsRequired();
            entity.Property(e => e.ContributorId).IsRequired();
            entity.Property(e => e.NameToGuess).IsRequired().HasMaxLength(100);

            entity.HasOne(p => p.GameRound)
                  .WithMany(g => g.NameAssignments)
                  .HasForeignKey(p => p.GameRoundId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuestionAnswer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GameRoundId).IsRequired();
            entity.Property(e => e.QuestionerId).IsRequired();
            entity.Property(e => e.ResponderId).IsRequired();
            entity.Property(e => e.Question).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(q => q.GameRound)
                  .WithMany(g => g.Answers)
                  .HasForeignKey(q => q.GameRoundId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
} 