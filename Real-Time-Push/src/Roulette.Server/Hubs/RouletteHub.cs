using Microsoft.AspNetCore.SignalR;
using Roulette.Server.Game;

namespace Roulette.Server.Hubs;

// CONCEPT: A Hub is the server endpoint that clients call (client -> server RPC) and that pushes
// messages back (server -> client). A NEW hub instance is created for every call and disposed
// right after, so it holds no state. All state lives in GameStore.
// (see docs/02-signalr-concepts.md#hubs)
//
// Phase 1 uses string message names ("BetPlaced"). Phase 2 switches to a strongly-typed Hub<IRouletteClient>.
public sealed class RouletteHub(GameStore store, ILogger<RouletteHub> logger) : Hub
{
    // ----- Connection lifecycle ----------------------------------------------------------------

    // CONCEPT: Runs once per connection, after the transport (WebSocket, SSE or long polling) is
    // established. (see docs/02-signalr-concepts.md#connection-lifecycle)
    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Connected: {ConnectionId}", Context.ConnectionId);

        // CONCEPT: Clients.Caller = only the connection that triggered this call.
        // A welcome message is relevant to nobody else. (see docs/02-signalr-concepts.md#sending-to-clients)
        await Clients.Caller.SendAsync("Welcome",
            new WelcomeEvent(Context.ConnectionId, GameStore.TableIds, store.Jackpot));

        await base.OnConnectedAsync();
    }

    // CONCEPT: Runs when the connection closes for any reason: the tab is closed, the network drops,
    // or the client stops answering pings (exception != null in that case). SignalR removes the
    // connection from all its groups automatically. We only clean up OUR state.
    // (see docs/02-signalr-concepts.md#connection-lifecycle)
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Disconnected: {ConnectionId} ({Reason})",
            Context.ConnectionId, exception?.Message ?? "clean close");

        var tableId = store.RemovePlayer(Context.ConnectionId);
        if (tableId is not null)
            await NotifyPlayersChanged(tableId);

        await base.OnDisconnectedAsync(exception);
    }

    // ----- Client -> server methods (called with connection.invoke("JoinTable", ...)) --------------

    public async Task JoinTable(string tableId, string nickname)
    {
        nickname = nickname?.Trim() ?? "";

        // CONCEPT: Throwing a HubException rejects the client's invoke() promise with this message.
        // Use it for "your call was invalid". (see docs/02-signalr-concepts.md#errors)
        if (!store.TableExists(tableId))
            throw new HubException($"Unknown table '{tableId}'.");
        if (nickname.Length is 0 or > 20)
            throw new HubException("Nickname must be 1 to 20 characters.");

        // A player sits at one table at a time, so leave the old one first.
        var previousTable = store.LeaveTable(Context.ConnectionId);
        if (previousTable is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, previousTable);
            await NotifyPlayersChanged(previousTable);
        }

        // CONCEPT: A Group is a named set of connections, here one per table. We join the group BEFORE
        // taking the snapshot so no table event falls into the gap between the two. A small ordering
        // race is still possible (an event can arrive before the snapshot). Phase 2 fixes it with
        // sequence numbers. (see docs/02-signalr-concepts.md#groups)
        await Groups.AddToGroupAsync(Context.ConnectionId, tableId);
        var snapshot = store.JoinTable(Context.ConnectionId, nickname, tableId);

        await Clients.Caller.SendAsync("Joined", snapshot);
        await NotifyPlayersChanged(tableId);

        logger.LogInformation("{Nickname} ({ConnectionId}) joined {TableId}", nickname, Context.ConnectionId, tableId);
    }

    public async Task LeaveTable()
    {
        var tableId = store.LeaveTable(Context.ConnectionId);
        if (tableId is null)
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tableId);
        await Clients.Caller.SendAsync("Left", new { tableId });
        await NotifyPlayersChanged(tableId);
    }

    public async Task PlaceBet(int amount, string target)
    {
        var outcome = store.PlaceBet(Context.ConnectionId, amount, target, DateTimeOffset.UtcNow);

        if (!outcome.Accepted)
        {
            // CONCEPT: A rejected bet is a normal business outcome, not a crash, so we PUSH it to the
            // caller as a message rather than throwing. Only the person who placed the bet needs to know.
            await Clients.Caller.SendAsync("BetRejected", new BetRejectedEvent(amount, target, outcome.Error!));
            return;
        }

        // CONCEPT: Clients.Group(tableId) = everyone at this table, including the caller.
        // Everyone at the table sees every bet. (see docs/02-signalr-concepts.md#sending-to-clients)
        await Clients.Group(outcome.TableId).SendAsync("BetPlaced", outcome.Placed);

        // CONCEPT: The balance is private, so it goes to the caller only. Inside a hub, Caller is the
        // connection that placed the bet. Phase 2 switches to Clients.User(...) so that ALL tabs of the
        // same logged-in player get it. (see docs/02-signalr-concepts.md#sending-to-clients)
        await Clients.Caller.SendAsync("BalanceUpdated", new BalanceEvent(outcome.Balance));

        // CONCEPT: Clients.All = every connected client on every table. The jackpot is global.
        // Phase 1 broadcasts on EVERY bet; Phase 2 throttles this to at most once per second.
        // (see docs/03-casino-realtime-patterns.md#message-criticality)
        await Clients.All.SendAsync("JackpotUpdated", new JackpotEvent(outcome.Jackpot));
    }

    private Task NotifyPlayersChanged(string tableId) =>
        Clients.Group(tableId).SendAsync("PlayersChanged", new PlayersChangedEvent(tableId, store.PlayersAt(tableId)));
}
