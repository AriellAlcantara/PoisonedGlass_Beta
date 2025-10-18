# Rock-Paper-Scissors Game - Quick Setup Guide

## Unity Scene Setup (3 GameObjects Required)

### 1. NetworkManager GameObject
**Components:**
- `GameManager.cs`

**Inspector Settings:**
- Current Game Mode: `Host` or `Client`
- Player Prefab: Assign your networked player prefab
- Button: Assign the "Start Game" button
- Input: Optional text input field

---

### 2. GameHandler GameObject
**Components:**
- `NetworkObject` (REQUIRED - this is a Fusion component)
- `GameHandler.cs`
- `GameStateMachine.cs` (will auto-add if missing)

**Inspector Settings:**
- No fields to assign - leave everything empty
- DO NOT assign Rock/Paper/Scissor buttons here

**Important:** This GameObject must be spawned by Fusion as a NetworkObject. Either:
- Place it in the scene with a NetworkObject component, OR
- Create a prefab and spawn it via Fusion

---

### 3. GameUIManager GameObject
**Components:**
- `GameUIManager.cs`

**Inspector Settings:**
- **Selection UI:**
  - Selection UI: Parent GameObject containing the 3 choice buttons
  - Rock Button: Button for Rock selection
  - Paper Button: Button for Paper selection
  - Scissor Button: Button for Scissors selection

- **Result UI:**
  - Win UI: GameObject to show when player wins
  - Lose UI: GameObject to show when player loses
  - Draw UI: GameObject to show on draw

- **Game Info UI:**
  - Round Number Text: TextMeshPro text showing current round
  - Player Count Text: TextMeshPro text showing player count

- **Root UI Container:**
  - Game UI Root: (Optional) Parent GameObject containing all game UI
    - If assigned, entire container is hidden until connected to network
    - If not assigned, individual UI elements are hidden/shown separately

---

## Testing the Game

### Using ParrelSync (Recommended)
1. Install ParrelSync from Package Manager
2. Tools > ParrelSync > Create Clone
3. Open main project, click "Start Game" and select Host
4. Open clone project, click "Start Game" and select Client
5. When 2 players join, game starts automatically

### Testing Checklist
- [ ] 2 players can join
- [ ] Round number displays and increments
- [ ] Player count shows "2 Players"
- [ ] Rock/Paper/Scissors buttons appear each round
- [ ] Results show correctly (Win/Lose/Draw)
- [ ] Game loops to next round after 3 seconds

---

## How It Works (Simplified)

1. **Start Application** → All game UI is hidden (not connected)
2. **Click "Start Game"** → Connect as Host or Client
3. **Connected to Network** → Game UI becomes visible
4. **Players Join** → GameManager spawns players and tracks them
5. **2+ Players?** → Game starts automatically
6. **Each Round:**
   - Show Rock/Paper/Scissors buttons
   - Players make selections
   - Both selected? → Evaluate winner
   - Show results for 3 seconds
   - Loop back to next round

---

## Troubleshooting

**Buttons don't work:**
- Check that GameUIManager has all button references assigned
- Verify buttons have Button component attached

**Game doesn't start:**
- Ensure GameHandler has NetworkObject component
- Check that 2 players have joined (look at player count text)
- Verify GameManager has correct player prefab assigned

**UI doesn't update:**
- Check that GameUIManager is in the scene and active
- Verify UI GameObjects are assigned in inspector

**Network errors:**
- Ensure Fusion App ID is configured in Fusion settings
- Check that both Host and Client are in the same session ("TestRoom")

---

## Architecture Overview

```
GameManager (Networking)
    ├─> Spawns players
    ├─> Publishes: PlayerJoinedEvent, PlayerLeftEvent
    └─> Handles input collection

GameHandler (Game Logic)
    ├─> Manages state machine
    ├─> Evaluates Rock/Paper/Scissors
    ├─> Publishes: RoundStartedEvent, RoundEndedEvent
    └─> Only runs on Host (StateAuthority)

GameUIManager (UI)
    ├─> Listens to all events
    ├─> Shows/hides UI elements
    ├─> Handles button clicks
    └─> Calls GameHandler.SendPlayerSelection()
```

---

## What Was Fixed

1. Removed button references from GameHandler (they belong in UI)
2. Fixed player count to 2 (was incorrectly set to 3)
3. Added GameStartedEvent publication when game begins
4. Added player count tracking in UI (updates live)
5. Connected UI buttons to GameHandler properly
6. Simplified button click flow: UI → GameHandler → RPC
7. Added network connection tracking (UI hidden until connected)
8. Game UI only shows after successfully joining as Host or Client

---

## Files You Need

- ✅ All Event files (EventBus.cs, IGameEvent.cs, GameEvents.cs)
- ✅ GameManager.cs
- ✅ GameHandler.cs
- ✅ GameStateMachine.cs, GameState.cs, IGameState.cs
- ✅ GameUIManager.cs
- ✅ Your networked player prefab

Optional:
- ScoreManager.cs (score tracking)
- ScoreDisplayUI.cs (score display)
