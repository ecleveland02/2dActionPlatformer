namespace Margin.Level
{
    /// <summary>Static level events (spec 3.2 event bus). Listeners must unsubscribe when disabled.</summary>
    public static class LevelEvents
    {
        /// <summary>The player is now in this room (door, respawn or scene start).</summary>
        public static event System.Action<Room> RoomEntered;
        /// <summary>The player touched an ink pot: new respawn point, health restored.</summary>
        public static event System.Action<Checkpoint> CheckpointReached;
        /// <summary>The player was moved instantly (door, pit, respawn): cameras jump instead of gliding there.</summary>
        public static event System.Action CameraCut;

        public static void RaiseRoomEntered(Room room) => RoomEntered?.Invoke(room);
        public static void RaiseCheckpointReached(Checkpoint checkpoint) => CheckpointReached?.Invoke(checkpoint);
        public static void RaiseCameraCut() => CameraCut?.Invoke();
    }
}
