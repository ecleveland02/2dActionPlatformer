namespace Margin.Core
{
    /// <summary>
    /// Something that goes back to its starting state whenever its room is entered again (enemies, bosses).
    /// Rooms call it on everything below them when the player walks in or respawns there.
    /// </summary>
    public interface IRoomReset
    {
        void ResetForRoom();
    }
}
