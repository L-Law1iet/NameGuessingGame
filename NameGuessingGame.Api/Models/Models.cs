using System.ComponentModel.DataAnnotations.Schema;

namespace NameGuessingGame.Api.Models;

public class User
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ConnectionId { get; set; }
    public Guid? RoomId { get; set; }
}

public class Room
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public GameStatus Status { get; set; } = GameStatus.Waiting;
    public int MaxPlayers { get; set; } = 10;
    public List<User> Players { get; set; } = new List<User>();
    public List<GameRound> GameRounds { get; set; } = new List<GameRound>();
}

public enum GameStatus
{
    Waiting,
    Preparation,
    Playing,
    Finished
}

public class GameRound
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public int RoundNumber { get; set; }
    public Guid CurrentPlayerId { get; set; }
    public int CurrentPlayerIndex { get; set; }
    public string? PlayerTurnOrder { get; set; }
    public string? CurrentQuestion { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    [ForeignKey("RoomId")]
    public virtual Room? Room { get; set; }
    public virtual ICollection<PlayerNameAssignment> NameAssignments { get; set; } = new List<PlayerNameAssignment>();
    public virtual ICollection<QuestionAnswer> Answers { get; set; } = new List<QuestionAnswer>();
}

public class PlayerNameAssignment
{
    public Guid Id { get; set; }
    public Guid GameRoundId { get; set; }
    public GameRound GameRound { get; set; } = null!;
    public Guid PlayerId { get; set; }
    public Guid ContributorId { get; set; }
    public string NameToGuess { get; set; } = string.Empty;
    public bool HasGuessedCorrectly { get; set; } = false;
}

public class QuestionAnswer
{
    public Guid Id { get; set; }
    public Guid GameRoundId { get; set; }
    public GameRound GameRound { get; set; } = null!;
    public Guid QuestionerId { get; set; }
    public Guid ResponderId { get; set; }
    public string Question { get; set; } = string.Empty;
    public bool Answer { get; set; }
    public DateTime CreatedAt { get; set; }
} 