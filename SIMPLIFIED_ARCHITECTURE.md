# Rock-Paper-Scissors Game - Simplified Architecture

## What You Have

A clean, event-driven rock-paper-scissors game with Photon Fusion networking.

## Core Systems

### 1. Event Bus
**Files**: `Events/EventBus.cs`, `Events/IGameEvent.cs`, `Events/GameEvents.cs`

Simple publish-subscribe system for decoupled communication.

**Usage**:
```csharp
// Subscribe
EventBus.Subscribe<RoundStartedEvent>(OnRoundStarted);

// Publish
EventBus.Publish(new RoundStartedEvent { RoundNumber = 1 });

// Unsubscribe
EventBus.Unsubscribe<RoundStartedEvent>(OnRoundStarted);
```

### 2. Game State Machine
**Files**: `GameManager/GameStateMachine.cs`, `GameManager/GameState.cs`

Controls game flow through 6 states:
1. **WaitingForPlayers** → Need 2+ players
2. **RoundStarting** → Starting new round
3. **WaitingForSelections** → Players choose rock/paper/scissors
4. **Evaluating** → Determining winner
5. **ShowingResults** → Display results (3 seconds)
6. **RoundEnding** → Loop back to next round

### 3. Game Manager
**File**: `GameManager/GameManager.cs`

Handles:
- Photon Fusion networking
- Player spawning/despawning
- Input collection

### 4. Game Handler
**File**: `GameManager/GameHandler.cs`

Handles:
- Rock-paper-scissors logic
- State machine control
- Player selections
- Win/loss/draw evaluation

### 5. UI Manager
**File**: `UI/GameUIManager.cs`

Listens to events and shows/hides UI:
- Selection buttons (rock, paper, scissors)
- Win/loss/draw screens
- Round numbers

## Game Events

- `GameStartedEvent` - 2+ players joined
- `RoundStartedEvent` - New round begins
- `RoundEndedEvent` - Round finishes
- `PlayerJoinedEvent` - Player connects
- `PlayerLeftEvent` - Player disconnects
- `ShowSelectionUIEvent` - Show choice buttons
- `HideSelectionUIEvent` - Hide choice buttons
- `ShowResultUIEvent` - Show win/loss/draw
- `ScoreUpdatedEvent` - Score changes (optional feature)

## Optional: Score Tracking
**Files**: `GameManager/ScoreManager.cs`, `UI/ScoreDisplayUI.cs`

Tracks wins/losses/draws for each player.

## That's It!

Clean, simple, event-driven rock-paper-scissors game.

No chat, no persistence, no over-engineering - just what you need.
