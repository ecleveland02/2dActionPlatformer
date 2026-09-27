using Margin.Combat;
using Margin.Core;
using Margin.FX;
using Margin.Physics;
using Margin.Player;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Enemies
{
    /// <summary>
    /// Practice target (Milestone 3): takes hits, knockback, hitstun and hitstop, logs every hit, and shows a
    /// combo counter. A combo continues while each new hit lands before the previous hitstun runs out.
    /// It never dies, and drifts back home after being left alone for a while.
    /// </summary>
    [RequireComponent(typeof(KinematicBody2D))]
    public sealed class TrainingDummy : MonoBehaviour, IHitReceiver, ITickable
    {
        [Tooltip("Gravity and ground friction come from here (the player's MovementData is fine).")]
        [SerializeField] private MovementData physics;
        [SerializeField] private CombatSettings settings;
        [SerializeField] private StickFigureRig rig;
        [SerializeField] private PoseData idlePose;
        [SerializeField] private PoseData hitPose;
        [SerializeField] private TextMesh label;
        [Tooltip("Ticks without being hit before the dummy returns to where it started.")]
        [SerializeField, Min(1)] private int returnHomeAfterFrames = 180;

        private KinematicBody2D body;
        private Vector2 velocity;
        private Vector2 home;
        private int hitstop, hitstun, stagger, idleTicks;

        public int TickOrder => 20;
        public bool CanBeHit => true;
        public int Combo { get; private set; }
        public int ComboDamage { get; private set; }
        public int BestCombo { get; private set; }
        public int TotalDamage { get; private set; }
        public int HitstunRemaining => hitstun;
        public int StaggerRemaining => stagger;
        /// <summary>In hitstun or staggered (after being parried): can't act.</summary>
        public bool IsStunned => hitstun > 0 || stagger > 0;
        public int Facing { get; private set; } = -1;
        public StickFigureRig Rig => rig;
        public Vector2 Position => body.Position;
        /// <summary>Pose shown while not stunned (set by SparringAttacker during attacks). Null = idle pose.</summary>
        public FigurePose? PoseOverride { get; set; }
        public int HitstopRemaining => hitstop;
        public Vector2 Velocity => velocity;

        private CombatSettings Settings => settings != null ? settings : CombatSettings.Defaults;

        private void Awake()
        {
            body = GetComponent<KinematicBody2D>();
            home = transform.position;
            if (rig != null) Facing = rig.transform.localScale.x < 0f ? -1 : 1;
        }

        private void OnEnable() => GameLoop.Register(this);
        private void OnDisable() => GameLoop.Unregister(this);

        /// <summary>Parried (spec 6.5): open for a punish, shown with the hit pose.</summary>
        public void Stagger(int frames)
        {
            stagger = Mathf.Max(stagger, frames);
            idleTicks = 0;
            Debug.Log($"[Dummy] staggered for {frames} frames", this);
        }

        /// <summary>Turns to face left (-1) or right (+1). The visual is flipped; the hurtbox is symmetric.</summary>
        public void SetFacing(int direction)
        {
            Facing = direction < 0 ? -1 : 1;
            if (rig != null) rig.transform.localScale = new Vector3(Facing, 1f, 1f);
        }

        public void Configure(MovementData physicsData, StickFigureRig figure, TextMesh text)
        {
            physics = physicsData;
            rig = figure;
            label = text;
        }

        public bool ReceiveHit(in HitInfo hit)
        {
            bool comboContinues = hitstun > 0;
            if (!comboContinues)
            {
                Combo = 0;
                ComboDamage = 0;
            }

            Combo++;
            ComboDamage += hit.Damage;
            TotalDamage += hit.Damage;
            BestCombo = Mathf.Max(BestCombo, Combo);

            hitstun = hit.HitstunFrames;
            velocity = hit.Knockback;
            // A heavy hit already froze the whole game, so the dummy doesn't need its own freeze.
            hitstop = hit.GlobalHitstop ? 0 : hit.HitstopFrames;
            idleTicks = 0;

            int frame = GameLoop.Clock?.CurrentFrame ?? 0;
            Debug.Log($"[Dummy] f{frame}  {hit.Attack.name}  {hit.Damage} dmg  |  combo {Combo} ({ComboDamage} dmg)" +
                      (comboContinues ? "" : "  (new combo)"), this);
            UpdateLabel();
            return true;
        }

        public void Tick()
        {
            if (physics == null) return;

            if (hitstop > 0)
            {
                hitstop--;
                ShakeVisual(hitstop > 0);
                return;
            }
            ShakeVisual(false);

            bool grounded = body.Collisions.Grounded;
            if (grounded && velocity.y <= 0f)
            {
                velocity.y = 0f;
                velocity.x = MovementMath.Approach(velocity.x, 0f, physics.GroundDecelStep);
            }

            float dy = velocity.y * GameTime.TickDelta;
            if (!grounded || velocity.y > 0f)
            {
                // Juggle: airborne targets in hitstun fall slower so air combos can keep them up.
                float gravity = physics.FallGravity * (hitstun > 0 ? Settings.juggleGravityScale : 1f);
                // A spiked target (slammed downward) may fall faster than normal max fall speed.
                float maxFall = Mathf.Max(physics.maxFallSpeed, -velocity.y);
                velocity.y = MovementMath.VerticalStep(velocity.y, gravity, maxFall, out dy);
            }

            float fallSpeed = -velocity.y;
            body.Move(new Vector2(velocity.x * GameTime.TickDelta, dy));
            CollisionState c = body.Collisions;
            if (c.JustLanded && fallSpeed >= Settings.slamImpactSpeed) OnSlamImpact(fallSpeed);
            if (c.HitWallLeft || c.HitWallRight) velocity.x = 0f;
            if (c.HitCeiling && velocity.y > 0f) velocity.y = 0f;
            if (c.Grounded && velocity.y < 0f) velocity.y = 0f;

            if (hitstun > 0)
            {
                hitstun--;
                if (hitstun == 0) UpdateLabel();
            }
            if (stagger > 0) stagger--;

            ReturnHomeWhenIdle();
            if (rig != null)
            {
                if (IsStunned && hitPose != null) rig.ApplyPose(hitPose.pose);
                else if (PoseOverride.HasValue) rig.ApplyPose(PoseOverride.Value);
                else if (idlePose != null) rig.ApplyPose(idlePose.pose);
            }
        }

        /// <summary>Hit the ground hard after a spike: shake, ink burst from the feet, and a log line.</summary>
        private void OnSlamImpact(float speed)
        {
            CameraShake.Shake(0.2f);
            Vector2 feet = body.Position + new Vector2(0f, -0.9f);
            if (InkSplatter.Instance != null) InkSplatter.Instance.Burst(feet, Vector2.up, 20);
            Debug.Log($"[Dummy] SLAM impact at {speed:0.0} u/s", this);
        }

        private void ReturnHomeWhenIdle()
        {
            if (hitstun > 0 || ++idleTicks < returnHomeAfterFrames) return;
            if (Vector2.Distance(body.Position, home) > 0.5f)
            {
                body.Teleport(home);
                velocity = Vector2.zero;
            }
            idleTicks = 0;
        }

        private void ShakeVisual(bool shaking)
        {
            if (rig == null) return;
            // Alternate left/right each tick during hitstop (spec 6.4: target shakes 0.05 units).
            float x = shaking ? (hitstop % 2 == 0 ? 1f : -1f) * Settings.hitShakeDistance : 0f;
            rig.transform.localPosition = new Vector3(x, 0f, 0f);
        }

        private void UpdateLabel()
        {
            if (label == null) return;
            string state = hitstun > 0 ? $"COMBO {Combo}" : $"combo {Combo} (dropped)";
            label.text = $"{state}   {ComboDamage} dmg\nbest {BestCombo}   total {TotalDamage}";
        }
    }
}
