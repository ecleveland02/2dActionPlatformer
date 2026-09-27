using Margin.Core;
using UnityEngine;

namespace Margin.Level
{
    /// <summary>
    /// One room of a world (spec 11.1, Hollow Knight style): its geometry, enemies, doors and checkpoints are all
    /// children of this object. Only the room the player is in is active; the LevelDirector switches rooms when the
    /// player walks through a RoomDoor. The camera never shows anything outside <see cref="WorldBounds"/>
    /// (the room's confiner), and falling below it counts as a pit.
    /// Entering a room resets everything in it that implements IRoomReset (enemies come back).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Room : MonoBehaviour
    {
        [Tooltip("Name shown in the debug overlay. Empty = the object's name.")]
        [SerializeField] private string title;
        [Tooltip("The area the camera may show, relative to this object's position. Its bottom edge is also the " +
                 "pit line: fall far enough below it and you are put back at the last door.")]
        [SerializeField] private Rect bounds = new Rect(-15f, -2f, 30f, 16f);

        private RoomDoor[] doors;

        public string Title => string.IsNullOrEmpty(title) ? name : title;

        /// <summary>Camera bounds in world space.</summary>
        public Rect WorldBounds
        {
            get
            {
                Vector2 p = transform.position;
                return new Rect(bounds.position + p, bounds.size);
            }
        }

        /// <summary>Camera bounds relative to the room (set by the level builder).</summary>
        public Rect LocalBounds
        {
            get => bounds;
            set => bounds = value;
        }

        /// <summary>Every door in the room, including ones on inactive objects.</summary>
        public RoomDoor[] Doors
        {
            get
            {
                if (doors == null) doors = GetComponentsInChildren<RoomDoor>(true);
                return doors;
            }
        }

        public void Configure(string roomTitle, Rect cameraBounds)
        {
            title = roomTitle;
            bounds = cameraBounds;
        }

        /// <summary>Puts enemies (and anything else that implements IRoomReset) back to their starting state.</summary>
        public void ResetContents()
        {
            foreach (IRoomReset r in GetComponentsInChildren<IRoomReset>(true)) r.ResetForRoom();
        }

        public bool Contains(Vector2 point) => WorldBounds.Contains(point);

        /// <summary>The room an object belongs to (its nearest Room parent), even while that room is switched off.</summary>
        public static Room Of(Component component)
        {
            for (Transform t = component != null ? component.transform : null; t != null; t = t.parent)
            {
                var room = t.GetComponent<Room>();
                if (room != null) return room;
            }
            return null;
        }

        private void OnDrawGizmos()
        {
            Rect b = WorldBounds;
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.9f);
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
