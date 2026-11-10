# Lab 00 — Setup

**Goal:** have every tool installed and verified, and a clean workspace for all labs.

**Time:** ~30 minutes.

---

## Step 1 — Install the tools

Run these in **PowerShell** (Windows). Skip anything you already have.

| Tool | Why | Install |
|------|-----|---------|
| **.NET 10 SDK** | All lab code | `winget install Microsoft.DotNet.SDK.10` |
| **Docker Desktop** | Run RabbitMQ, Kafka, Service Bus emulator locally | `winget install Docker.DockerDesktop` (then start it; WSL 2 backend) |
| **Git** | Version your lab work | `winget install Git.Git` |
| **Editor** | VS Code + C# Dev Kit, or Visual Studio / Rider | `winget install Microsoft.VisualStudioCode` |
| **SQLite CLI** (Lab 03) | Inspect outbox/inbox tables | `winget install SQLite.SQLite` — or the VS Code extension "SQLite Viewer" |
| **Azure CLI** (Lab 05, optional) | Create a real Service Bus namespace | `winget install Microsoft.AzureCLI` |

> On macOS/Linux: install the same tools with Homebrew / your package manager. All lab commands work the same except paths.

## Step 2 — Verify

```powershell
dotnet --version          # 10.x
docker --version
docker run --rm hello-world
git --version
sqlite3 --version         # optional
az --version              # optional
```

**What you should see:** versions printed and the Docker "Hello from Docker!" message. If `docker run` fails, make sure Docker Desktop is running.

## Step 3 — Create the workspace

```powershell
mkdir C:\Labs\messaging
cd C:\Labs\messaging
git init
dotnet new gitignore
```

Each lab gets its own folder: `C:\Labs\messaging\lab01-rabbitmq`, `lab02-failures`, …

Create a journal file:

```powershell
New-Item NOTES.md
```

Write in it after every lab: *what I expected / what happened / what I learned*.

## Step 4 — Pull the images ahead of time (optional, saves time later)

```powershell
docker pull rabbitmq:4-management
docker pull apache/kafka:4.0.0
```

(The Service Bus emulator images are pulled in Lab 05.)

## Step 5 — Docker cheat sheet you'll use in every lab

```powershell
docker ps                         # running containers
docker ps -a                      # all containers
docker logs -f <name>             # follow logs
docker stop <name>                # stop (data kept)
docker start <name>               # start again
docker restart <name>             # simulate a broker restart
docker rm -f <name>               # delete the container (data gone unless in a named volume)
docker exec -it <name> bash       # shell inside the container
```

## Step 6 — .NET cheat sheet

```powershell
dotnet new sln -n LabXX
dotnet new console -n Producer
dotnet new worker  -n Consumer
dotnet new webapi  -n Orders.Api
dotnet new classlib -n Contracts
dotnet sln add (Get-ChildItem -Recurse *.csproj)
dotnet add Producer package RabbitMQ.Client
dotnet add Producer reference Contracts
dotnet run --project Producer
```

> Tip: to run several apps at once, open several terminal tabs (Windows Terminal: `Ctrl+Shift+T`). Many experiments need 3–4 terminals side by side.

---

## ✅ Checkpoint

- [ ] `dotnet --version` shows 10.x
- [ ] `docker run hello-world` works
- [ ] `C:\Labs\messaging` exists with a `NOTES.md`

Next: [Lab 01 — RabbitMQ Fundamentals](01-RabbitMQ-Fundamentals.md)
