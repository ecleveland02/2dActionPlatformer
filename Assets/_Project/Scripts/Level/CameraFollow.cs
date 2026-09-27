using Margin.Player;
using UnityEngine;

namespace Margin.Level
{
    /// <summary>
    /// The game camera (spec 12). Follows the target with:
    ///   - a slight look-ahead in the direction the player faces (CameraSettings.lookAhead, spec 0.5 units),
    ///   - a dead zone: small sideways, larger up/down, so the view doesn't bob on every jump,
    ///   - grounded recentering: standing on something eases the view's height back onto the player,
    ///   - a room confiner: never shows anything outside the LevelDirector's current room.
    /// Door, pit and respawn teleports (LevelEvents.CameraCut) jump the view instead of gliding.
    ///
    /// Written instead of Cinemachine (spec 2 lists it): no package to add, and it counts in game frames, so it
    /// freezes with pause and hitstop and slows with the F5 slow motion. The math is in CameraMath (unit tested).
    /// Runs in LateUpdate so it follows the interpolated (smooth) player position; CameraShake adds its offset after.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private CameraSettings settings;

        private Camera cam;
        private PlayerController player;
        private Vector2 aim;          // where the camera looks, before shake
        private float lead;           // current look-ahead
        private Vector3 lastTargetPosition;
        private int moveDirection = 1;
        private bool snap = true;

        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                snap = true;
            }
        }

        public CameraSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        private CameraSettings S => settings != null ? settings : CameraSettings.Defaults;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam != null && S.orthographicSize > 0f) cam.orthographicSize = S.orthographicSize;
        }

        private void OnEnable() => LevelEvents.CameraCut += Snap;
        private void OnDisable() => LevelEvents.CameraCut -= Snap;

        /// <summary>Jump straight to the target next frame instead of easing there.</summary>
        public void Snap() => snap = true;

        private void LateUpdate()
        {
            if (target == null) return;
            if (player == null || player.transform != target) player = target.GetComponent<PlayerController>();

            CameraSettings s = S;
            float frames = Time.deltaTime * 60f;   // game frames this render frame (0 while paused)

            // Look-ahead toward the facing direction (or the last direction moved, for a non-player target).
            Vector3 p = target.position;
            if (Mathf.Abs(p.x - lastTargetPosition.x) > 0.0001f) moveDirection = p.x > lastTargetPosition.x ? 1 : -1;
            lastTargetPosition = p;
            int facing = player != null ? player.Facing : moveDirection;
            float leadGoal = facing * s.lookAhead;
            lead = snap ? leadGoal : CameraMath.Approach(lead, leadGoal, s.lookAheadFrames, frames);

            Vector2 goal = (Vector2)p + s.offset + new Vector2(lead, 0f);
            if (snap)
            {
                aim = goal;
            }
            else
            {
                float half = s.deadZoneWidth * 0.5f;
                aim.x = CameraMath.DeadZone(aim.x, goal.x, half, half);
                bool grounded = player == null || player.Body == null || player.Grounded;
                if (grounded) aim.y = CameraMath.Approach(aim.y, goal.y, s.groundedRecenterFrames, frames);
                aim.y = CameraMath.DeadZone(aim.y, goal.y, s.deadZoneDown, s.deadZoneUp);
            }

            // Room confiner. The aim itself is clamped, so walking back from a wall moves the view right away.
            Room room = LevelDirector.Instance != null ? LevelDirector.Instance.CurrentRoom : null;
            if (room != null && s.confineToRoom && cam != null && cam.orthographic)
            {
                float halfHeight = cam.orthographicSize;
                float halfWidth = halfHeight * cam.aspect;
                Rect b = room.WorldBounds;
                aim.x = CameraMath.Confine(aim.x, halfWidth, b.xMin, b.xMax);
                aim.y = CameraMath.Confine(aim.y, halfHeight, b.yMin, b.yMax);
            }

            transform.position = new Vector3(aim.x, aim.y, transform.position.z);
            snap = false;
        }
    }
}
