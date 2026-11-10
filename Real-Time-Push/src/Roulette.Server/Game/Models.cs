namespace Roulette.Server.Game;

// ---------------------------------------------------------------------------
// Messages pushed to clients. They are records, so they are immutable and
// safe to hand to SignalR, which serializes them on another thread.
// Property names reach JavaScript in camelCase (roundId, endsAtUtc, ...).
// ---------------------------------------------------------------------------

public enum RoundPhase
{
    BettingOpen,
    BettingClosed,
    Result
}

// CONCEPT: We send the END TIME of the phase plus the server's current time, never "seconds left".
// The client works out its countdown from these two values, so a slow message or a busy browser
// tab cannot extend the betting window. (see docs/03-casino-realtime-patterns.md#server-authoritative-state-and-timing)
public record RoundState(
    string TableId,
    long RoundId,
    RoundPhase Phase,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset ServerNowUtc);

public record BetInfo(string Nickname, int Amount, string Target);

public record BetPlacedEvent(string TableId, long RoundId, BetInfo Bet);

public record BetRejectedEvent(int Amount, string Target, string Reason);

public record RoundResultEvent(string TableId, long RoundId, int Number, string Color);

// Private to one player: how this round went for them.
public record PlayerRoundResult(long RoundId, int Number, int TotalBet, int TotalPayout, int Net, int Balance);

public record BalanceEvent(int Balance);

public record JackpotEvent(decimal Amount);

public record PlayersChangedEvent(string TableId, string[] Players);

public record WelcomeEvent(string ConnectionId, string[] Tables, decimal Jackpot);

// Everything a client needs to draw a table from scratch, sent right after it joins.
public record TableSnapshot(
    string TableId,
    string Nickname,
    int Balance,
    decimal Jackpot,
    RoundState Round,
    string[] Players,
    BetInfo[] Bets,
    RoundResultEvent? LastResult);
