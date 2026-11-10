namespace Roulette.Server.Game;

// Mutable server-side state. Only GameStore touches these, and only while holding its lock.
// Clients never see these classes; they get the immutable records from Models.cs.

public sealed class Table(string id)
{
    public string Id { get; } = id;
    public long RoundId { get; set; }
    public RoundPhase Phase { get; set; } = RoundPhase.BettingClosed;
    public DateTimeOffset EndsAtUtc { get; set; }
    public List<PlacedBet> Bets { get; } = [];
    public RoundResultEvent? LastResult { get; set; }
}

public sealed record PlacedBet(string ConnectionId, string Nickname, int Amount, string Target);

// Phase 1: a player is identified by their SignalR connection id. That means a page refresh
// creates a new player with a fresh balance. Phase 2 replaces this with a real user id from a login token.
public sealed class Player(string connectionId, string nickname)
{
    public string ConnectionId { get; } = connectionId;
    public string Nickname { get; set; } = nickname;
    public string? TableId { get; set; }
    public int Balance { get; set; } = GameStore.StartingBalance;
}
