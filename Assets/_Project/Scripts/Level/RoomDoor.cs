using UnityEngine;

namespace Margin.Level
{
    /// <summary>
    /// An exit from a room (spec 11.1). When the player's body touches the door's area, the screen fades, the next
    /// room is switched on and the player arrives at the target door's arrival point, already moving in.
    /// Put side doors in a gap in the room's wall at the edge of the camera bounds; put the arrival point inside the
    /// room, clear of the door's own area (otherwise the player would walk straight back out).
    ///
    /// Side says which edge of the room the door is on. Arriving through a Left door the player walks right;
    /// through a Bottom door (coming up from the room below) they are pushed up and sideways (arrivalNudge) onto the
    /// ledge beside the hole; through a Top door they drop in.
    /// </summary>
    public sealed class RoomDoor : MonoBehaviour
    {
        public enum Side { Left, Right, Top, Bottom }

        [SerializeField] private Side side;
        [Tooltip("The door in the next room this one leads to (it usually leads back here).")]
        [SerializeField] private RoomDoor target;
        [Tooltip("Leave the world: with no Target, walking through loads this scene (it must be in Build Settings).")]
        [SerializeField] private string nextScene;
        [Tooltip("Size of the trigger area, centered on this object.")]
        [SerializeField] private Vector2 size = new Vector2(1f, 4f);
        [Tooltip("Where the player's center appears when arriving through this door, relative to this object.")]
        [SerializeField] private Vector2 arrivalOffset = new Vector2(1.5f, 1f);
        [Tooltip("Arriving from below: sideways drift (-1 left, 0 straight up, +1 right) toward the ledge.")]
        [SerializeField, Range(-1, 1)] private int arrivalNudge;
        [Tooltip("Where a pit fall puts the player back after arriving here, relative to this object. " +
                 "Zero = the arrival point. Set it for Top/Bottom doors, whose arrival point is in mid-air.")]
        [SerializeField] private Vector2 safeOffset;

        private Room room;

        public Side DoorSide => side;
        public RoomDoor Target => target;
        public string NextScene => nextScene;
        /// <summary>Leads somewhere: another door, or another scene.</summary>
        public bool Leads => target != null || !string.IsNullOrEmpty(nextScene);
        public int ArrivalNudge => arrivalNudge;

        /// <summary>The room this door belongs to.</summary>
        public Room Room
        {
            get
            {
                if (room == null) room = Room.Of(this);
                return room;
            }
        }

        /// <summary>Trigger area in world space.</summary>
        public Rect Area => new Rect((Vector2)transform.position - size * 0.5f, size);
        public Vector2 ArrivalPoint => (Vector2)transform.position + arrivalOffset;
        public Vector2 SafePoint => safeOffset == Vector2.zero ? ArrivalPoint : (Vector2)transform.position + safeOffset;

        /// <summary>Direction the player walks when arriving: +1 through a Left door, -1 through a Right door.</summary>
        public int InwardDirection => side == Side.Left ? 1 : side == Side.Right ? -1 : 0;

        /// <summary>Set by the level builder.</summary>
        public void Configure(Side doorSide, Vector2 areaSize, Vector2 arrival, int nudge = 0, Vector2 safe = default)
        {
            side = doorSide;
            size = areaSize;
            arrivalOffset = arrival;
            arrivalNudge = nudge;
            safeOffset = safe;
        }

        /// <summary>Makes this door the way out of the world, into another scene (e.g. World 1 to World 2).</summary>
        public void ConfigureSceneExit(string scene) => nextScene = scene;

        /// <summary>Connects two doors both ways.</summary>
        public static void Link(RoomDoor a, RoomDoor b)
        {
            a.target = b;
            b.target = a;
        }

        private void OnDrawGizmos()
        {
            Rect a = Area;
            Gizmos.color = new Color(0.1f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireCube(a.center, a.size);
            Gizmos.DrawWireSphere(ArrivalPoint, 0.25f);
            if (target != null)
            {
                Gizmos.color = new Color(0.1f, 0.7f, 1f, 0.35f);
                Gizmos.DrawLine(a.center, target.Area.center);
            }
        }
    }
}
