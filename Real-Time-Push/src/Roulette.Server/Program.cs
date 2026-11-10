using System.Text.Json.Serialization;
using Roulette.Server.Game;
using Roulette.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddSignalR(options =>
    {
        // CONCEPT: In Development, send real exception messages to the client so you can see
        // why a hub call failed. In Production keep this off so internals don't leak.
        // (see docs/02-signalr-concepts.md#configuration-settings)
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    })
    .AddJsonProtocol(options =>
    {
        // Send enums as "BettingOpen" instead of 0 so the event log is readable.
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// CONCEPT: GameStore is a singleton because it is the single source of truth for game state.
// Hubs are transient (a new instance per call), so they must never hold state themselves.
// (see docs/02-signalr-concepts.md#hubs)
builder.Services.AddSingleton<GameStore>();

// CONCEPT: The round loop runs on the server in a BackgroundService, not in the browser.
// The server owns the clock. (see docs/03-casino-realtime-patterns.md#server-authoritative-state-and-timing)
builder.Services.AddHostedService<GameEngine>();

var app = builder.Build();

// Serves wwwroot/index.html at "/" and the JS/CSS next to it.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<RouletteHub>("/hubs/roulette");

app.Run();
