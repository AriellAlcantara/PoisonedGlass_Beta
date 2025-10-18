# Fusion 2 Rock-Paper-Scissors Game

A fully networked multiplayer rock-paper-scissors game built with Photon Fusion 2, featuring modern architecture patterns.

## Features

- Networked multiplayer using Photon Fusion 2
- Event-driven architecture for decoupled systems
- Finite State Machine for clear game flow
- Score tracking system
- Clean separation between game logic and UI
- Comprehensive documentation

## Quick Start

### Setup
1. Open the Main scene: `Assets/Scenes/Main.unity`
2. Attach the following components in the scene:
   - GameManager (with GameManager.cs)
   - GameHandler (as NetworkObject with GameHandler.cs)
   - GameUIManager (with GameUIManager.cs)
   - ScoreManager (with ScoreManager.cs) - optional
3. Configure UI references in inspector

### Testing Multiplayer
1. Use ParrelSync to create a clone project (Tools > ParrelSync)
2. Run main project as Host
3. Run clone project as Client
4. Both players join and can play rock-paper-scissors

## Architecture

### Core Systems

**Event Bus** (`Assets/Scripts/Events/`)
- Decoupled communication between systems
- No singleton dependencies
- Type-safe events

**Game State Machine** (`Assets/Scripts/GameManager/GameStateMachine.cs`)
- Manages game flow through clear states
- Network-synchronized state transitions
- Easy to debug and extend

**Game Manager** (`Assets/Scripts/GameManager/GameManager.cs`)
- Handles Fusion networking
- Spawns/despawns players
- Processes input

**Game Handler** (`Assets/Scripts/GameManager/GameHandler.cs`)
- Core game logic
- Evaluates rock-paper-scissors outcomes
- Manages state machine

**UI Manager** (`Assets/Scripts/UI/GameUIManager.cs`)
- Listens to game events
- Updates UI without knowing game logic
- Shows/hides UI elements

**Score Manager** (`Assets/Scripts/GameManager/ScoreManager.cs`)
- Tracks player statistics
- Calculates win rates
- Example of easy extensibility

### File Structure
```
Assets/Scripts/
├── Events/
│   ├── EventBus.cs          # Central event system
│   ├── IGameEvent.cs        # Event marker interface
│   └── GameEvents.cs        # All event definitions
├── GameManager/
│   ├── GameManager.cs       # Networking & player management
│   ├── GameHandler.cs       # Game logic
│   ├── GameStateMachine.cs  # FSM implementation
│   ├── GameState.cs         # State enum
│   ├── IGameState.cs        # State interface
│   └── ScoreManager.cs      # Score tracking
├── UI/
│   ├── GameUIManager.cs     # Main UI controller
│   ├── ScoreDisplayUI.cs    # Score display component
│   └── ChatUI.cs            # Chat system
├── Player/
│   ├── Player.cs            # Player controller
│   ├── Health.cs            # Health system
│   └── ICombat.cs           # Combat interface
├── Projectile/
│   └── BulletProjectile.cs  # Bullet behavior
├── Input/
│   └── NetworkInputData.cs  # Network input structure
├── ARCHITECTURE.md          # Detailed architecture docs
└── README.md                # This file
```

## Game States

1. **WaitingForPlayers** - Waiting for 2+ players to join
2. **RoundStarting** - Incrementing round, publishing events
3. **WaitingForSelections** - Players choose rock/paper/scissors
4. **Evaluating** - Determining winner
5. **ShowingResults** - Displaying outcome (3 seconds)
6. **RoundEnding** - Cleaning up, looping to next round

## Key Events

### Game Events
- `GameStartedEvent` - Game begins with 2+ players
- `RoundStartedEvent` - New round begins
- `RoundEndedEvent` - Round finishes with result

### Player Events
- `PlayerJoinedEvent` - Player connects
- `PlayerLeftEvent` - Player disconnects
- `PlayerMadeSelectionEvent` - Player chooses rock/paper/scissors

### UI Events
- `ShowSelectionUIEvent` - Show choice buttons
- `HideSelectionUIEvent` - Hide choice buttons
- `ShowResultUIEvent` - Show win/loss/draw result
- `HideResultUIEvent` - Hide results

### Score Events
- `ScoreUpdatedEvent` - Player's score changes

## Adding New Features

### Example: Add Sound Effects

1. Create event in `GameEvents.cs`:
```csharp
public struct PlaySoundEvent : IGameEvent
{
    public string SoundName;
}
```

2. Create SoundManager:
```csharp
public class SoundManager : MonoBehaviour
{
    void OnEnable()
    {
        EventBus.Subscribe<PlaySoundEvent>(OnPlaySound);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<PlaySoundEvent>(OnPlaySound);
    }

    void OnPlaySound(PlaySoundEvent evt)
    {
        // Play sound
    }
}
```

3. Publish from game logic:
```csharp
EventBus.Publish(new PlaySoundEvent { SoundName = "win" });
```

That's it! No existing code needs to change.

## Best Practices

### Event Bus Usage
```csharp
// Always subscribe in OnEnable
void OnEnable()
{
    EventBus.Subscribe<MyEvent>(OnMyEvent);
}

// Always unsubscribe in OnDisable
void OnDisable()
{
    EventBus.Unsubscribe<MyEvent>(OnMyEvent);
}
```

### Network Authority
```csharp
// Only state authority should modify game state
if (Object.HasStateAuthority)
{
    _stateMachine.TransitionToState(GameState.NextState);
}
```

### RPC Usage
```csharp
// Broadcast to all clients
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
void RPC_BroadcastEvent() { }

// Send to specific player
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
void RPC_ShowResult([RpcTarget] PlayerRef player) { }
```

## Troubleshooting

### Game doesn't start
- Check that 2+ players have joined
- Verify GameStateMachine is initialized
- Check state authority is working

### UI doesn't update
- Verify GameUIManager is subscribed to events
- Check UI references are set in inspector
- Look for EventBus subscription errors

### Networking issues
- Ensure Fusion App ID is configured
- Check firewall settings
- Verify NetworkRunner is created properly

## Credits

Built with:
- Unity 2022.3+
- Photon Fusion 2
- TextMeshPro
- ParrelSync (for testing)

## License

Educational project - free to use and modify.
