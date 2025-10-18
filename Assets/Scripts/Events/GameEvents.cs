using Fusion;

namespace GNW2.Events
{
    // ===== Game State Events =====

    public struct GameStartedEvent : IGameEvent
    {
        public int PlayerCount;
    }

    public struct RoundStartedEvent : IGameEvent
    {
        public int RoundNumber;
    }

    public struct RoundEndedEvent : IGameEvent
    {
        public PlayerRef Winner;
        public bool IsDraw;
    }

    // ===== Player Events =====

    public struct PlayerJoinedEvent : IGameEvent
    {
        public PlayerRef Player;
        public NetworkObject PlayerObject;
    }

    public struct PlayerLeftEvent : IGameEvent
    {
        public PlayerRef Player;
    }

    public struct PlayerMadeSelectionEvent : IGameEvent
    {
        public PlayerRef Player;
        public int Selection; // 0 = Rock, 1 = Paper, 2 = Scissors
    }

    public struct PlayerDamagedEvent : IGameEvent
    {
        public PlayerRef Player;
        public int Damage;
        public float CurrentHealth;
    }

    // ===== UI Events =====

    public struct ShowSelectionUIEvent : IGameEvent
    {
        public PlayerRef TargetPlayer;
    }

    public struct HideSelectionUIEvent : IGameEvent
    {
    }

    public struct ShowResultUIEvent : IGameEvent
    {
        public PlayerRef TargetPlayer;
        public bool IsWin;
        public bool IsDraw;
    }

    public struct HideResultUIEvent : IGameEvent
    {
    }

    // ===== Combat Events =====

    public struct BulletFiredEvent : IGameEvent
    {
        public PlayerRef Shooter;
        public NetworkObject Bullet;
    }

    public struct BulletHitEvent : IGameEvent
    {
        public NetworkObject Bullet;
        public NetworkObject Target;
    }

    // ===== Score Events =====

    public struct ScoreUpdatedEvent : IGameEvent
    {
        public PlayerRef Player;
        public int Wins;
        public int Losses;
        public int Draws;
    }

    // ===== Network Connection Events =====

    public struct NetworkConnectedEvent : IGameEvent
    {
        public bool IsHost;
    }

    public struct NetworkDisconnectedEvent : IGameEvent
    {
    }

}
