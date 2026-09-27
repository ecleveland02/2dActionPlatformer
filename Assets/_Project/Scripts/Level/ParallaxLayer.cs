using UnityEngine;

namespace Margin.Level
{
    /// <summary>
    /// A decoration layer that scrolls at its own speed for depth (spec 11.3): faint notebook lines 0.9,
    /// midground doodles 0.6, foreground smudges 1.1. 1 moves with the level; lower looks farther away; above 1
    /// passes in front. The layer sits at its authored position when the camera is at the room's center, so lay
    /// decorations out as they should look from there (and a little past the room's edges for the slow layers).
    /// </summary>
    [DefaultExecutionOrder(900)]   // after the camera has moved (CameraShake at 1000 adds its offset later)
    public sealed class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("How fast this layer scrolls compared with the level: 1 = with it, 0.6 = far behind, 1.1 = in front.")]
        [SerializeField, Range(0f, 2f)] private float scroll = 0.9f;

        private Vector3 home;
        private bool hasHome;
        private Room room;

        public float Scroll
        {
            get => scroll;
            set => scroll = value;
        }

        private void OnEnable()
        {
            if (!hasHome)
            {
                home = transform.position;
                hasHome = true;
            }
            room = Room.Of(this);
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector2 reference = room != null ? room.WorldBounds.center : (Vector2)home;
            Vector2 offset = ((Vector2)cam.transform.position - reference) * (1f - scroll);
            transform.position = new Vector3(home.x + offset.x, home.y + offset.y, home.z);
        }
    }
}
