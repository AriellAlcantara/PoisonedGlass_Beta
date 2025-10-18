# Game Architecture Documentation

## Overview
This Fusion 2 networked rock-paper-scissors game uses an **Event-Driven Architecture** with a **Finite State Machine (FSM)** for clean, decoupled, and maintainable code.

## Core Patterns

### 1. Event Bus Pattern
**Location**: `Assets/Scripts/Events/`

The event bus allows systems to communicate without direct dependencies:
- **EventBus.cs**: Central event dispatcher
- **IGameEvent.cs**: Marker interface for all events
- **GameEvents.cs**: Definitions of all game events

**Benefits**:
- Decoupled systems
- Easy to add new features
- No singleton dependencies
- Better testability

**Usage Example**:
```csharp
// Subscribe to an event
EventBus.Subscribe<PlayerJoinedEvent>(OnPlayerJoined);

// Publish an event
EventBus.Publish(new PlayerJoinedEvent { Player = player });

// Unsubscribe (important!)
EventBus.Unsubscribe<PlayerJoinedEvent>(OnPlayerJoined);
```

### 2. Finite State Machine (FSM)
**Location**: `Assets/Scripts/GameManager/GameStateMachine.cs`

The game flow is managed through distinct states:

1. **WaitingForPlayers** - Waiting for minimum 2 players
2. **RoundStarting** - Beginning a new round
3. **WaitingForSelections** - Players making rock/paper/scissors choice
4. **Evaluating** - Determining winner/loser/draw
5. **ShowingResults** - Displaying outcome for 3 seconds
6. **RoundEnding** - Cleaning up and preparing next round

**Benefits**:
- Clear game flow
- Easy to debug (can see current state)
- Prevents invalid transitions
- Easy to extend with new states

## System Architecture

### GameManager
**Responsibility**: Networking & Player Management
- Manages Photon Fusion NetworkRunner
- Spawns/despawns players on join/leave
- Collects and sends input to network
- Publishes player join/leave events

### GameHandler
**Responsibility**: Game Logic
- Manages state machine
- Handles player selections
- Evaluates rock-paper-scissors outcomes
- Publishes game events (round start/end, results)

### GameStateMachine
**Responsibility**: Game Flow Control
- Controls which state game is in
- Handles state transitions
- Manages timing (e.g., 3 second result display)
- Only state authority can transition states

### GameUIManager
**Responsibility**: UI Updates
- Listens to game events
- Shows/hides UI elements
- Updates round number and player count
- Completely decoupled from game logic

## Event Flow Examples

### Player Joins
```
1. Player connects to Fusion session
2. GameManager.OnPlayerJoined() called
3. Spawns player object
4. Publishes PlayerJoinedEvent
5. GameUIManager updates player count display
```

### Round Flow
```
1. State: WaitingForPlayers
   └─> 2+ players? → Transition to RoundStarting

2. State: RoundStarting
   ├─> Increment round number
   ├─> Publish RoundStartedEvent
   └─> Transition to WaitingForSelections

3. State: WaitingForSelections
   ├─> Publish ShowSelectionUIEvent (GameUIManager shows UI)
   ├─> Players click rock/paper/scissors buttons
   ├─> RPC_SendTurn called for each player
   └─> Both selected? → Transition to Evaluating

4. State: Evaluating
   ├─> Compare selections
   ├─> Determine winner
   ├─> Publish ShowResultUIEvent for each player
   └─> Transition to ShowingResults

5. State: ShowingResults
   ├─> Display results for 3 seconds
   └─> Timer expires? → Transition to RoundEnding

6. State: RoundEnding
   ├─> Clean up
   └─> Loop back to RoundStarting
```

## Networking Architecture

### Authority Model
- **State Authority** (Host): Controls game state, FSM, evaluation
- **Input Authority** (Client): Controls their own player movement/shooting

### RPC Usage
- **[Rpc(StateAuthority, All)]**: Broadcast events to all clients
- **[Rpc(All, StateAuthority)]**: Send player input to server
- **[RpcTarget]**: Send targeted messages to specific players

### Networked Variables
- `GameState CurrentState` - Current FSM state
- `int CurrentRound` - Current round number
- `int PlayersReady` - How many players have made selections

## Adding New Features

### Adding a New Event
1. Define in `GameEvents.cs`:
```csharp
public struct MyNewEvent : IGameEvent
{
    public string SomeData;
}
```

2. Publish from logic:
```csharp
EventBus.Publish(new MyNewEvent { SomeData = "test" });
```

3. Subscribe in UI/systems:
```csharp
EventBus.Subscribe<MyNewEvent>(OnMyNewEvent);
```

### Adding a New State
1. Add to `GameState` enum
2. Create state class implementing `IGameState`
3. Register in `GameStateMachine.Initialize()`
4. Add transition logic

### Adding Score Tracking
See `ScoreManager.cs` example (bonus system):
- Subscribes to `RoundEndedEvent`
- Tracks wins/losses per player
- Publishes `ScoreUpdatedEvent`
- UI can display scores without knowing game logic

## Best Practices

### DO:
- ✅ Always unsubscribe from events in OnDisable/OnDestroy
- ✅ Use events for cross-system communication
- ✅ Use RPC for networked actions
- ✅ Add XML documentation to public methods
- ✅ Use [SerializeField] for inspector fields
- ✅ Check Object.HasStateAuthority before state changes

### DON'T:
- ❌ Access singleton instances directly in logic
- ❌ Mix UI code with game logic
- ❌ Forget to handle authority in networked games
- ❌ Create tight coupling between systems
- ❌ Skip documentation

## Debugging Tips

### Check Current State
The FSM logs every state transition:
```
State Transition: WaitingForSelections -> Evaluating
```

### Event Flow
All events are published through EventBus - add debug logging there:
```csharp
Debug.Log($"Event Published: {typeof(T).Name}");
```

### Network Issues
Check:
- Does state authority have control?
- Are RPCs configured correctly?
- Is Object spawned correctly?

## Performance Considerations

### Event Bus
- Very lightweight (dictionary lookup)
- No garbage allocation for events (structs)
- Clear() when changing scenes

### FSM
- Only runs on state authority
- Minimal overhead per state
- States can be as simple or complex as needed

## Future Extensions

Possible improvements:
- Tournament mode (best of 5)
- Player statistics persistence
- Cosmetic customization
- Power-ups/special moves
- Spectator mode
- Matchmaking system
