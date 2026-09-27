namespace Margin.Player
{
    /// <summary>Static player events (spec 3.2 event bus). Listeners must unsubscribe when disabled.</summary>
    public static class PlayerEvents
    {
        /// <summary>Health reached 0; the defeat animation starts.</summary>
        public static event System.Action<PlayerController> Died;
        /// <summary>The player is back at the spawn point with full health. Enemies reset on this.</summary>
        public static event System.Action<PlayerController> Respawned;

        public static void RaiseDied(PlayerController player) => Died?.Invoke(player);
        public static void RaiseRespawned(PlayerController player) => Respawned?.Invoke(player);
    }
}
