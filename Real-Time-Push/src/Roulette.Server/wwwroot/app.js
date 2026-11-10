"use strict";

// ---------------------------------------------------------------------------
// Client state. The client keeps only a COPY of what the server told it.
// The server is the source of truth.
// ---------------------------------------------------------------------------
const state = {
  tableId: null,
  roundId: null,
  phase: null,
  endsAtMs: 0,
  clockOffsetMs: 0, // serverTime - localTime
  bets: [],
};

const $ = (id) => document.getElementById(id);

// CONCEPT: The connection negotiates the best transport the browser and server both support
// (WebSockets, then Server-Sent Events, then long polling). Phase 1 has no automatic reconnect;
// Phase 2 adds it. (see docs/02-signalr-concepts.md#transports-and-negotiation)
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/roulette")
  .configureLogging(signalR.LogLevel.Information)
  .build();

// ---------------------------------------------------------------------------
// Event log: every message in and out is shown so you can SEE the push traffic.
// ---------------------------------------------------------------------------
function log(kind, name, payload) {
  const icons = { in: "⬇", out: "⬆", error: "⚠" };
  const li = document.createElement("li");
  li.className = kind;

  const head = document.createElement("div");
  const time = document.createElement("span");
  time.className = "time";
  time.textContent = new Date().toISOString().substring(11, 23) + " ";
  const label = document.createElement("span");
  label.className = "name";
  label.textContent = `${icons[kind]} ${name}`;
  head.append(time, label);

  const body = document.createElement("pre");
  body.textContent = typeof payload === "string" ? payload : JSON.stringify(payload);

  li.append(head, body);
  const list = $("log");
  list.prepend(li);
  while (list.children.length > 300) list.lastChild.remove();
}

// Register a server -> client handler and log every message it receives.
function on(name, handler) {
  connection.on(name, (payload) => {
    log("in", name, payload);
    handler(payload);
  });
}

// Call a hub method and log it. A HubException thrown on the server rejects this promise.
async function invoke(name, ...args) {
  log("out", name, args);
  try {
    return await connection.invoke(name, ...args);
  } catch (err) {
    log("error", name, err.message ?? String(err));
    throw err;
  }
}

// ---------------------------------------------------------------------------
// Server -> client messages
// ---------------------------------------------------------------------------
on("Welcome", (msg) => {
  $("connection-id").textContent = `id: ${msg.connectionId}`;
  renderJackpot(msg.jackpot);
});

on("Joined", (snapshot) => {
  state.tableId = snapshot.tableId;
  state.bets = snapshot.bets;
  applyRoundState(snapshot.round);
  renderBalance(snapshot.balance);
  renderJackpot(snapshot.jackpot);
  renderPlayers(snapshot.players);
  renderBets();
  if (snapshot.lastResult) renderResult(snapshot.lastResult);
  $("current-table").textContent = `At ${snapshot.tableId} as ${snapshot.nickname}.`;
  $("leave-btn").disabled = false;
});

on("Left", () => {
  state.tableId = null;
  state.phase = null;
  state.bets = [];
  $("current-table").textContent = "Not at a table.";
  $("leave-btn").disabled = true;
  renderPlayers([]);
  renderBets();
  updateBetButton();
});

on("PlayersChanged", (msg) => {
  if (msg.tableId === state.tableId) renderPlayers(msg.players);
});

on("RoundStateChanged", (round) => {
  if (round.tableId !== state.tableId) return;
  if (round.phase === "BettingOpen" && round.roundId !== state.roundId) {
    state.bets = []; // a new round starts with an empty bet list
    renderBets();
    $("bet-message").textContent = "";
  }
  applyRoundState(round);
});

on("BetPlaced", (msg) => {
  if (msg.tableId !== state.tableId || msg.roundId !== state.roundId) return;
  state.bets.push(msg.bet);
  renderBets();
});

on("BetRejected", (msg) => showBetMessage(`Rejected by server: ${msg.reason}`, "error"));

on("BalanceUpdated", (msg) => {
  renderBalance(msg.balance);
  showBetMessage("Bet accepted.", "ok");
});

on("RoundResult", (msg) => {
  if (msg.tableId === state.tableId) renderResult(msg);
});

on("PlayerRoundResult", (msg) => {
  renderBalance(msg.balance);
  const verb = msg.net > 0 ? `won ${msg.net}` : msg.net < 0 ? `lost ${-msg.net}` : "broke even";
  $("my-result").textContent =
    `Round ${msg.roundId}: you bet ${msg.totalBet}, got back ${msg.totalPayout}, so you ${verb}.`;
});

on("JackpotUpdated", (msg) => renderJackpot(msg.amount));

// ---------------------------------------------------------------------------
// Server-authoritative countdown
// ---------------------------------------------------------------------------

// CONCEPT: We never count down from a local "10 seconds" timer. The server sends the phase END TIME
// and its CURRENT TIME. We store the clock difference and show endsAt - serverNow. If this tab freezes
// or the message is late, the countdown is still correct when it wakes up.
// (see docs/03-casino-realtime-patterns.md#server-authoritative-state-and-timing)
function applyRoundState(round) {
  state.roundId = round.roundId;
  state.phase = round.phase;
  state.endsAtMs = Date.parse(round.endsAtUtc);
  state.clockOffsetMs = Date.parse(round.serverNowUtc) - Date.now();
  $("phase").textContent = round.phase;
  $("round-id").textContent = `round #${round.roundId}`;
  updateBetButton();
}

function serverNowMs() {
  return Date.now() + state.clockOffsetMs;
}

setInterval(() => {
  if (!state.phase) {
    $("countdown").textContent = "—";
    return;
  }
  const remaining = Math.max(0, state.endsAtMs - serverNowMs());
  $("countdown").textContent = (remaining / 1000).toFixed(1) + "s";
  updateBetButton();
}, 100);

function updateBetButton() {
  const open = state.phase === "BettingOpen" && serverNowMs() < state.endsAtMs;
  // The button state is only a convenience. The server checks again and has the final say.
  $("bet-btn").disabled = !state.tableId || (!open && !$("force-enable").checked);
}

// ---------------------------------------------------------------------------
// Rendering
// ---------------------------------------------------------------------------
function renderBalance(balance) {
  $("balance").textContent = balance;
}

function renderJackpot(amount) {
  $("jackpot").textContent = Number(amount).toFixed(2);
}

function renderPlayers(players) {
  const ul = $("players");
  ul.replaceChildren(...players.map((name) => {
    const li = document.createElement("li");
    li.textContent = name;
    return li;
  }));
}

function renderBets() {
  const ul = $("bets");
  ul.replaceChildren(...state.bets.map((bet) => {
    const li = document.createElement("li");
    li.textContent = `${bet.nickname}: ${bet.amount} on ${bet.target}`;
    return li;
  }));
}

function renderResult(result) {
  const box = $("result");
  box.classList.remove("muted");
  const num = document.createElement("span");
  num.className = `num ${result.color}`;
  num.textContent = result.number;
  box.replaceChildren(`Round ${result.roundId} result: `, num, ` ${result.color}`);
}

function showBetMessage(text, kind) {
  const el = $("bet-message");
  el.textContent = text;
  el.className = `message ${kind}`;
}

// ---------------------------------------------------------------------------
// UI wiring
// ---------------------------------------------------------------------------
function fillTargets() {
  const select = $("target");
  const options = ["Red", "Black", ...Array.from({ length: 37 }, (_, n) => String(n))];
  select.replaceChildren(...options.map((value) => {
    const opt = document.createElement("option");
    opt.value = value;
    opt.textContent = /^\d+$/.test(value) ? `Number ${value}` : value;
    return opt;
  }));
}

$("join-btn").addEventListener("click", () => {
  invoke("JoinTable", $("table-select").value, $("nickname").value).catch(() => {});
});

$("leave-btn").addEventListener("click", () => invoke("LeaveTable").catch(() => {}));

$("bet-btn").addEventListener("click", () => {
  // Number() can produce a decimal like 10.5. The hub expects an int, so the call would fail
  // on the server. Round here only to keep the demo friendly.
  const amount = Math.round(Number($("amount").value));
  invoke("PlaceBet", amount, $("target").value).catch(() => {});
});

$("force-enable").addEventListener("change", updateBetButton);
$("clear-log").addEventListener("click", () => $("log").replaceChildren());

function setStatus(text, kind) {
  const el = $("connection-status");
  el.textContent = text;
  el.className = `badge ${kind ?? ""}`;
}

connection.onclose((err) => {
  setStatus("disconnected (refresh the page; Phase 2 adds auto-reconnect)", "bad");
  log("error", "connection closed", err?.message ?? "closed");
  $("bet-btn").disabled = true;
});

async function start() {
  fillTargets();
  $("nickname").value = "player" + Math.floor(Math.random() * 1000);
  try {
    await connection.start();
    // To see which transport was chosen, open DevTools > Network > WS, or read the console log.
    setStatus("connected", "ok");
  } catch (err) {
    setStatus("failed to connect", "bad");
    log("error", "start", err.message ?? String(err));
  }
}

start();
