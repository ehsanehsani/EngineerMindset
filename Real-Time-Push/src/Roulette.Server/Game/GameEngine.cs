using System.Security.Cryptography;
using Microsoft.AspNetCore.SignalR;
using Roulette.Server.Hubs;

namespace Roulette.Server.Game;

// Runs the round loop for every table:
//   BettingOpen (10s) -> BettingClosed (2s) -> Result + Payout (shown for 5s) -> next round
//
// CONCEPT: A BackgroundService cannot use a Hub instance. A Hub only exists for the length of one
// client call and has a Context tied to that caller. Here there is no caller: the SERVER starts the
// message. IHubContext<RouletteHub> is the singleton "send from anywhere" API, and it offers the same
// Clients.Group / Client / All targets. (see docs/02-signalr-concepts.md#ihubcontext)
public sealed class GameEngine(
    GameStore store,
    IHubContext<RouletteHub> hub,
    ILogger<GameEngine> logger) : BackgroundService
{
    private static readonly TimeSpan BettingOpenDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan BettingClosedDuration = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ResultDuration = TimeSpan.FromSeconds(5);

    // One independent loop per table, all running at the same time.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(GameStore.TableIds.Select(tableId => RunTableAsync(tableId, stoppingToken)));

    private async Task RunTableAsync(string tableId, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunOneRoundAsync(tableId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break; // the app is shutting down
            }
            catch (Exception ex)
            {
                // Never let one failed round kill the table loop forever.
                logger.LogError(ex, "Round failed on {TableId}; starting a new round", tableId);
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
    }

    private async Task RunOneRoundAsync(string tableId, CancellationToken ct)
    {
        // 1. Betting open
        var open = store.OpenBetting(tableId, BettingOpenDuration);
        await hub.Clients.Group(tableId).SendAsync("RoundStateChanged", open, ct);
        await DelayUntil(open.EndsAtUtc, ct);

        // 2. Betting closed: no new bets. The "no more bets" pause before the spin.
        var closed = store.CloseBetting(tableId, BettingClosedDuration);
        await hub.Clients.Group(tableId).SendAsync("RoundStateChanged", closed, ct);
        await DelayUntil(closed.EndsAtUtc, ct);

        // 3. Result + payout. CONCEPT: The SERVER picks the number with a cryptographic RNG.
        // The client only displays it. (see docs/03-casino-realtime-patterns.md#fairness-and-audit-logging)
        var number = RandomNumberGenerator.GetInt32(0, 37);
        var settlement = store.Settle(tableId, number, ResultDuration);
        logger.LogInformation("{TableId} round {RoundId}: {Number} {Color}, {Count} player(s) paid",
            tableId, settlement.Result.RoundId, number, settlement.Result.Color, settlement.Payouts.Count);

        await hub.Clients.Group(tableId).SendAsync("RoundStateChanged", settlement.State, ct);
        await hub.Clients.Group(tableId).SendAsync("RoundResult", settlement.Result, ct);

        // CONCEPT: Win/loss is private. Clients.Client(connectionId) targets exactly one connection.
        // There is no Caller here because no client made this call.
        // (see docs/02-signalr-concepts.md#sending-to-clients)
        foreach (var payout in settlement.Payouts)
            await hub.Clients.Client(payout.ConnectionId).SendAsync("PlayerRoundResult", payout.Result, ct);

        await DelayUntil(settlement.State.EndsAtUtc, ct);
    }

    // Wait until an absolute time instead of "sleep N seconds", so small delays don't add up across phases.
    private static Task DelayUntil(DateTimeOffset endsAtUtc, CancellationToken ct)
    {
        var remaining = endsAtUtc - DateTimeOffset.UtcNow;
        return remaining > TimeSpan.Zero ? Task.Delay(remaining, ct) : Task.CompletedTask;
    }
}
