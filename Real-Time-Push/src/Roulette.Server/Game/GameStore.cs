namespace Roulette.Server.Game;

public record BetOutcome(
    bool Accepted,
    string? Error,
    string TableId,
    BetPlacedEvent? Placed,
    int Balance,
    decimal Jackpot);

public record PlayerPayout(string ConnectionId, PlayerRoundResult Result);

public record Settlement(RoundState State, RoundResultEvent Result, List<PlayerPayout> Payouts);

// The single source of truth for all game state in Phase 1 (in memory, one server).
//
// CONCEPT: Both the hub (many concurrent client calls) and the GameEngine (the round timer) change
// this state at the same time. One lock keeps every check-then-change step atomic, e.g.
// "is betting open AND is the balance enough -> debit". Every critical section is a few
// microseconds of in-memory work, so one lock is simple and fast enough here.
// In Phase 3 this state moves to Redis and the lock becomes a Lua script.
// (see docs/03-casino-realtime-patterns.md#server-authoritative-state-and-timing)
public sealed class GameStore
{
    public const int StartingBalance = 1000;
    public const decimal JackpotRate = 0.01m;
    public static readonly string[] TableIds = ["table-1", "table-2"];

    private readonly object _gate = new();
    private readonly Dictionary<string, Table> _tables = TableIds.ToDictionary(id => id, id => new Table(id));
    private readonly Dictionary<string, Player> _players = new();
    private decimal _jackpot;

    public bool TableExists(string tableId) => _tables.ContainsKey(tableId);

    public decimal Jackpot
    {
        get { lock (_gate) return _jackpot; }
    }

    // ----- Players -------------------------------------------------------------------------

    public TableSnapshot JoinTable(string connectionId, string nickname, string tableId)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(connectionId, out var player))
            {
                player = new Player(connectionId, nickname);
                _players[connectionId] = player;
            }

            player.Nickname = nickname;
            player.TableId = tableId;
            return BuildSnapshot(player, _tables[tableId]);
        }
    }

    // Returns the table the player was at, or null if they were not at one.
    public string? LeaveTable(string connectionId)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(connectionId, out var player))
                return null;

            var previous = player.TableId;
            player.TableId = null;
            return previous;
        }
    }

    // Called when the connection closes. Returns the table the player was at, if any.
    public string? RemovePlayer(string connectionId)
    {
        lock (_gate)
        {
            if (!_players.Remove(connectionId, out var player))
                return null;

            return player.TableId;
        }
    }

    public string[] PlayersAt(string tableId)
    {
        lock (_gate)
        {
            return _players.Values
                .Where(p => p.TableId == tableId)
                .Select(p => p.Nickname)
                .Order()
                .ToArray();
        }
    }

    // ----- Bets ----------------------------------------------------------------------------

    // CONCEPT: Every rule is checked here on the server, inside the lock, at the moment the bet arrives.
    // The client's "Place bet" button being enabled proves nothing.
    // (see docs/03-casino-realtime-patterns.md#never-trust-the-client)
    public BetOutcome PlaceBet(string connectionId, int amount, string? rawTarget, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(connectionId, out var player) || player.TableId is null)
                return Reject("Join a table first.");

            var table = _tables[player.TableId];

            if (amount <= 0)
                return Reject("Amount must be greater than 0.", table.Id, player.Balance);

            if (!BetRules.TryNormalizeTarget(rawTarget, out var target))
                return Reject("Bet on Red, Black or a number from 0 to 36.", table.Id, player.Balance);

            // CONCEPT: Check BOTH the phase and the clock. The engine's timer can fire a few
            // milliseconds late, so the phase may still say "open" after the deadline has passed.
            // The deadline is what counts. (see docs/03-casino-realtime-patterns.md#server-authoritative-state-and-timing)
            if (table.Phase != RoundPhase.BettingOpen || nowUtc >= table.EndsAtUtc)
                return Reject("Betting is closed for this round.", table.Id, player.Balance);

            if (amount > player.Balance)
                return Reject($"Insufficient balance ({player.Balance}).", table.Id, player.Balance);

            player.Balance -= amount;
            table.Bets.Add(new PlacedBet(connectionId, player.Nickname, amount, target));
            _jackpot += amount * JackpotRate;

            var placed = new BetPlacedEvent(table.Id, table.RoundId, new BetInfo(player.Nickname, amount, target));
            return new BetOutcome(true, null, table.Id, placed, player.Balance, _jackpot);
        }

        static BetOutcome Reject(string reason, string tableId = "", int balance = 0) =>
            new(false, reason, tableId, null, balance, 0);
    }

    // ----- Round lifecycle (called only by GameEngine) ------------------------------------------

    public RoundState OpenBetting(string tableId, TimeSpan duration)
    {
        lock (_gate)
        {
            var table = _tables[tableId];
            table.RoundId++;
            table.Bets.Clear();
            return SetPhase(table, RoundPhase.BettingOpen, duration);
        }
    }

    public RoundState CloseBetting(string tableId, TimeSpan duration)
    {
        lock (_gate)
        {
            return SetPhase(_tables[tableId], RoundPhase.BettingClosed, duration);
        }
    }

    public Settlement Settle(string tableId, int number, TimeSpan resultDuration)
    {
        lock (_gate)
        {
            var table = _tables[tableId];
            var state = SetPhase(table, RoundPhase.Result, resultDuration);
            var result = new RoundResultEvent(table.Id, table.RoundId, number, BetRules.ColorOf(number));
            table.LastResult = result;

            var payouts = new List<PlayerPayout>();
            foreach (var playerBets in table.Bets.GroupBy(b => b.ConnectionId))
            {
                // A player who disconnected mid-round no longer exists in Phase 1, so their bet is lost.
                // Phase 2 fixes this by keying players on a real user id instead of a connection.
                if (!_players.TryGetValue(playerBets.Key, out var player))
                    continue;

                var totalBet = playerBets.Sum(b => b.Amount);
                var totalPayout = playerBets.Sum(b => BetRules.Payout(b.Target, b.Amount, number));
                player.Balance += totalPayout;

                payouts.Add(new PlayerPayout(
                    player.ConnectionId,
                    new PlayerRoundResult(table.RoundId, number, totalBet, totalPayout, totalPayout - totalBet, player.Balance)));
            }

            return new Settlement(state, result, payouts);
        }
    }

    // ----- Helpers (caller must hold _gate) --------------------------------------------------

    private static RoundState SetPhase(Table table, RoundPhase phase, TimeSpan duration)
    {
        var now = DateTimeOffset.UtcNow;
        table.Phase = phase;
        table.EndsAtUtc = now + duration;
        return new RoundState(table.Id, table.RoundId, phase, table.EndsAtUtc, now);
    }

    private TableSnapshot BuildSnapshot(Player player, Table table)
    {
        var round = new RoundState(table.Id, table.RoundId, table.Phase, table.EndsAtUtc, DateTimeOffset.UtcNow);
        var players = _players.Values.Where(p => p.TableId == table.Id).Select(p => p.Nickname).Order().ToArray();
        var bets = table.Bets.Select(b => new BetInfo(b.Nickname, b.Amount, b.Target)).ToArray();
        return new TableSnapshot(table.Id, player.Nickname, player.Balance, _jackpot, round, players, bets, table.LastResult);
    }
}
