using UnityEngine;

namespace Margin.Physics
{
    /// <summary>
    /// Moves a box through the level using Physics2D box casts, without a physics simulation
    /// (spec 5.1: no dynamic Rigidbody2D). The owner (e.g. PlayerController) decides how far to move
    /// each tick; this component works out how far it can actually go and what it touched.
    ///
    /// Key ideas:
    /// - The body tracks its own Position. Casts start from that position, never from the Transform,
    ///   so several moves in one tick (or in a test) always see the latest position.
    /// - Casts use a box shrunk by skinWidth on every side. A body resting on the floor therefore has
    ///   its real box touching the floor while the cast box is skinWidth above it, so casts never
    ///   start already overlapping the surface you stand on.
    /// - Horizontal and vertical movement are resolved separately (spec 5.1).
    /// - The Rigidbody2D is Kinematic with interpolation on, so rendering is smooth between 60 Hz ticks.
    ///
    /// Assumes the object is not rotated or scaled.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public sealed class KinematicBody2D : MonoBehaviour
    {
        [SerializeField] private KinematicBodyData data;

        // Nudge step for corner correction, in units. Internal precision, not a tuning value.
        private const float CornerStep = 0.01f;
        // Minimum ticks to ignore one-way platforms after starting a drop-through.
        private const int MinDropThroughTicks = 2;

        private BoxCollider2D box;
        private Rigidbody2D body;
        private CollisionState state;
        private Collider2D groundCollider;      // what we are standing on (valid while grounded)
        private Collider2D dropThroughCollider; // one-way platform being dropped through, or null
        private int dropThroughTicks;
        private IMovingSolid carrier;           // moving ground last carried on, and its stamp then
        private int carriedStamp;
        private Collider2D pushedBy;             // ignored by casts during Push (the body starts inside it)
        private static readonly System.Collections.Generic.List<KinematicBody2D> all = new System.Collections.Generic.List<KinematicBody2D>();

        // Reused buffers so casting does not allocate memory every tick.
        private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
        private readonly Collider2D[] overlaps = new Collider2D[8];
        private ContactFilter2D filter;

        public KinematicBodyData Data
        {
            get => data;
            set => data = value;
        }

        public CollisionState Collisions => state;
        public Vector2 Position { get; private set; }
        public Vector2 Size => box.size;
        public bool IsDroppingThrough => dropThroughCollider != null;
        /// <summary>What the body is standing on (null when airborne).</summary>
        public Collider2D GroundCollider => state.Grounded ? groundCollider : null;
        /// <summary>Every enabled body (moving blocks push them).</summary>
        public static System.Collections.Generic.IReadOnlyList<KinematicBody2D> All => all;

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        private void OnDisable() => all.Remove(this);

        private float Skin => data.skinWidth;
        private Vector2 CastSize => box.size - 2f * Skin * Vector2.one;

        private void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            Position = body.position;
        }

        /// <summary>Instantly places the body somewhere (respawn, tests). Clears collision state.</summary>
        public void Teleport(Vector2 position)
        {
            Position = position;
            body.position = position;
            transform.position = position;
            state = default;
            groundCollider = null;
            dropThroughCollider = null;
        }

        /// <summary>
        /// Moves without testing for collisions (a flying boss gliding over platforms). Unlike Teleport the motion
        /// stays interpolated. Collision state is cleared: the body is treated as airborne.
        /// </summary>
        /// <summary>
        /// Pushed by something solid (a moving block). Moves with collisions like Move, but keeps the owner's
        /// collision state. Returns how far it actually went (less than asked = squeezed against a wall).
        /// </summary>
        public Vector2 Push(Vector2 delta, Collider2D pusher = null)
        {
            EnsureData();
            pushedBy = pusher;
            Vector2 start = Position, p = Position;
            if (delta.x != 0f) p = MoveHorizontal(p, delta.x);
            if (delta.y < 0f) p = MoveDown(p, -delta.y);
            else if (delta.y > 0f) p = MoveUp(p, delta.y, 0f);
            pushedBy = null;
            Position = p;
            body.MovePosition(p);
            return p - start;
        }

        public void MoveFree(Vector2 delta)
        {
            Position += delta;
            body.MovePosition(Position);
            state = default;
            groundCollider = null;
            dropThroughCollider = null;
        }

        /// <summary>
        /// Fall through the one-way platform currently underfoot (Down + Jump). Only that platform is ignored,
        /// so a stack of one-way platforms still catches you on the next one down.
        /// </summary>
        public void DropThroughOneWay()
        {
            if (!state.Grounded || !state.OnOneWay) return;
            dropThroughCollider = groundCollider;
            dropThroughTicks = 0;
            // No longer standing on anything, starting now (so the owner's state logic sees it this tick).
            state.Grounded = false;
            state.OnOneWay = false;
        }

        /// <summary>True if a wall (not a walkable slope) is directly beside the body. dir: -1 left, +1 right.</summary>
        public bool IsTouchingWall(int dir)
        {
            if (dir == 0) return false;
            EnsureData();
            Vector2 direction = new Vector2(dir, 0f);
            return Cast(Position, direction, Skin, data.solidMask, false, out RaycastHit2D hit)
                   && !IsWalkable(hit.normal);
        }

        /// <summary>
        /// Tries to move by <paramref name="delta"/> (units, this tick). Returns how far it actually moved.
        /// <paramref name="cornerCorrection"/>: when rising into a ceiling corner, how far (units) the body may
        /// be nudged sideways to slip past it. Pass 0 to disable.
        /// </summary>
        public Vector2 Move(Vector2 delta, float cornerCorrection = 0f)
        {
            EnsureData();
            bool wasGrounded = state.Grounded;
            Vector2 groundNormal = state.GroundNormal;
            state = default;
            state.WasGrounded = wasGrounded;

            // Standing on moving ground (a grid block): ride along first. It moved earlier this tick, so the body
            // is shifted straight to where it would be, without a cast (it's already resting on the surface).
            if (wasGrounded && groundCollider != null && groundCollider.TryGetComponent(out IMovingSolid solid))
            {
                if (solid != carrier || solid.MoveStamp != carriedStamp)
                {
                    Position += solid.CarryDelta;
                    carrier = solid;
                    carriedStamp = solid.MoveStamp;
                }
            }

            Vector2 start = Position;
            Vector2 p = Position;

            // 1. Horizontal. On the ground we slide along the ground surface so slopes feel like flat floor.
            if (delta.x != 0f)
            {
                if (wasGrounded && delta.y <= 0f) p = MoveAlongGround(p, delta.x, groundNormal);
                else p = MoveHorizontal(p, delta.x);
            }

            // 2. Vertical.
            if (delta.y < 0f) p = MoveDown(p, -delta.y);
            else if (delta.y > 0f) p = MoveUp(p, delta.y, cornerCorrection);

            // 3. Ground snap: stay glued to the ground over slope crests and small steps down.
            //    This also re-detects the ground every tick while standing still.
            if (wasGrounded && !state.Grounded && delta.y <= 0f) p = SnapToGround(p);

            UpdateDropThrough(p);

            Position = p;
            body.MovePosition(p);
            state.JustLanded = state.Grounded && !wasGrounded;
            return p - start;
        }

        private Vector2 MoveAlongGround(Vector2 p, float dx, Vector2 groundNormal)
        {
            float sign = Mathf.Sign(dx);
            float remaining = Mathf.Abs(dx);
            Vector2 dir = Tangent(groundNormal) * sign;

            // At most two segments: the current surface, then (after hitting a new walkable slope) that one.
            for (int segment = 0; segment < 2 && remaining > 0f; segment++)
            {
                if (!Cast(p, dir, remaining, data.solidMask, false, out RaycastHit2D hit))
                {
                    p += dir * remaining;
                    break;
                }

                float move = Mathf.Max(0f, hit.distance - Skin);
                p += dir * move;
                remaining -= move;

                if (IsWalkable(hit.normal))
                {
                    dir = Tangent(hit.normal) * sign;   // walk onto the new slope
                    continue;
                }

                SetWallHit(sign);
                break;
            }
            return p;
        }

        private Vector2 MoveHorizontal(Vector2 p, float dx)
        {
            Vector2 dir = new Vector2(Mathf.Sign(dx), 0f);
            float distance = Mathf.Abs(dx);

            if (!Cast(p, dir, distance, data.solidMask, false, out RaycastHit2D hit))
                return p + dir * distance;

            p += dir * Mathf.Max(0f, hit.distance - Skin);
            if (!IsWalkable(hit.normal)) SetWallHit(dir.x);
            return p;
        }

        private Vector2 MoveDown(Vector2 p, float distance)
        {
            if (!Cast(p, Vector2.down, distance, GroundMask, true, out RaycastHit2D hit))
                return p + Vector2.down * distance;

            float move = Mathf.Max(0f, hit.distance - Skin);
            p.y -= move;

            if (IsWalkable(hit.normal))
            {
                SetGround(hit);
                return p;
            }

            // Too steep to stand on: slide down along it with the leftover distance.
            float remaining = distance - move;
            Vector2 slide = Tangent(hit.normal);
            if (slide.y > 0f) slide = -slide;
            if (remaining > 0f && !Cast(p, slide, remaining, data.solidMask, false, out RaycastHit2D _))
                p += slide * remaining;
            return p;
        }

        private Vector2 MoveUp(Vector2 p, float distance, float cornerCorrection)
        {
            if (!Cast(p, Vector2.up, distance, data.solidMask, false, out RaycastHit2D hit))
                return p + Vector2.up * distance;

            if (cornerCorrection > 0f && TryCornerCorrection(ref p, distance, cornerCorrection))
                return p + Vector2.up * distance;

            p.y += Mathf.Max(0f, hit.distance - Skin);
            state.HitCeiling = true;
            return p;
        }

        /// <summary>
        /// Spec 5.2 corner correction: if the head only clips the edge of a ceiling, shift sideways
        /// (smallest shift first, trying both sides) so the jump continues instead of bonking.
        /// </summary>
        private bool TryCornerCorrection(ref Vector2 p, float upDistance, float maxShift)
        {
            for (float shift = CornerStep; shift <= maxShift + 0.0001f; shift += CornerStep)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 sideways = new Vector2(side, 0f);
                    if (Cast(p, sideways, shift, data.solidMask, false, out RaycastHit2D _)) continue;

                    // Check with the real box so the head ends up fully clear of the ceiling edge.
                    Vector2 shifted = p + sideways * shift;
                    if (Cast(shifted, Vector2.up, upDistance, data.solidMask, false, out RaycastHit2D _, fullSize: true)) continue;

                    p = shifted;
                    return true;
                }
            }
            return false;
        }

        private Vector2 SnapToGround(Vector2 p)
        {
            if (!Cast(p, Vector2.down, data.groundSnapDistance, GroundMask, true, out RaycastHit2D hit)) return p;
            if (!IsWalkable(hit.normal)) return p;

            p.y -= Mathf.Max(0f, hit.distance - Skin);
            SetGround(hit);
            return p;
        }

        // A missing data asset is reported once and replaced with defaults instead of throwing every tick.
        private void EnsureData()
        {
            if (data != null) return;
            Debug.LogError("KinematicBody2D: 'Data' is empty, so default collision values are being used. " +
                           "Drag Assets/_Project/Data/KinematicBodyData into it, or run Margin > Wire Player References.", this);
            data = ScriptableObject.CreateInstance<KinematicBodyData>();
        }

        private void UpdateDropThrough(Vector2 p)
        {
            if (dropThroughCollider == null) return;
            dropThroughTicks++;
            // Stop ignoring the platform once we have fully left it.
            if (dropThroughTicks >= MinDropThroughTicks && !Overlaps(p, dropThroughCollider)) dropThroughCollider = null;
        }

        private bool Overlaps(Vector2 p, Collider2D other)
        {
            filter.SetLayerMask(1 << other.gameObject.layer);
            int count = Physics2D.OverlapBox(p + box.offset, box.size, 0f, filter, overlaps);
            for (int i = 0; i < count; i++)
                if (overlaps[i] == other) return true;
            return false;
        }

        private LayerMask GroundMask => data.solidMask | data.oneWayMask;

        /// <summary>
        /// Box cast from position <paramref name="p"/>. Returns the nearest hit that blocks movement.
        /// One-way platforms only block when <paramref name="oneWayBlocks"/> is true (moving down), and only
        /// if the cast did not start inside them (that means we are passing up through it).
        /// Normally casts the skin-shrunk box; <paramref name="fullSize"/> casts the real box instead.
        /// </summary>
        private bool Cast(Vector2 p, Vector2 dir, float distance, LayerMask mask, bool oneWayBlocks,
                          out RaycastHit2D nearest, bool fullSize = false)
        {
            nearest = default;
            filter.SetLayerMask(mask);
            Vector2 size = fullSize ? box.size : CastSize;
            float castDistance = fullSize ? distance : distance + Skin;
            int count = Physics2D.BoxCast(p + box.offset, size, 0f, dir, filter, hits, castDistance);

            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = hits[i];
                if (hit.collider == box || hit.collider == pushedBy) continue;
                // Ignore surfaces we are moving along or away from (e.g. the slope we walk on).
                if (Vector2.Dot(hit.normal, dir) > -0.001f) continue;

                if (hit.collider == dropThroughCollider) continue;
                if (IsOneWay(hit.collider) && (!oneWayBlocks || hit.distance <= 0f)) continue;

                if (!found || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    found = true;
                }
            }
            return found;
        }

        private bool IsOneWay(Collider2D other)
        {
            return (data.oneWayMask.value & (1 << other.gameObject.layer)) != 0;
        }

        private bool IsWalkable(Vector2 normal)
        {
            return Vector2.Angle(normal, Vector2.up) <= data.maxSlopeAngle + 0.01f;
        }

        // Direction along a surface, pointing right. Flat ground (0,1) gives (1,0).
        private static Vector2 Tangent(Vector2 normal)
        {
            return new Vector2(normal.y, -normal.x);
        }

        private void SetGround(RaycastHit2D hit)
        {
            groundCollider = hit.collider;
            state.Grounded = true;
            state.GroundNormal = hit.normal;
            state.OnOneWay = IsOneWay(hit.collider);
        }

        private void SetWallHit(float dirX)
        {
            if (dirX < 0f) state.HitWallLeft = true;
            else state.HitWallRight = true;
        }
    }
}
