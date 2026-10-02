using System.Collections.Generic;
using Margin.Abilities;
using Margin.Combat;
using Margin.Core;
using Margin.FX;
using Margin.Physics;
using Margin.Player;
using Margin.Rendering;
using Margin.UI;
using UnityEngine;

namespace Margin.Enemies
{
    /// <summary>
    /// A ground enemy (spec 9): a stick figure with health that patrols, notices the player, approaches,
    /// attacks using AttackData (with combo strings), takes knockback, hitstun and juggles, and dies.
    /// Behavior lives in EnemyData, so the Doodle Grunt and Pencil Lancer are this component with different data.
    ///
    /// States: Patrol > Alert > Approach > Attack, plus Hitstun (hits, launches and parry staggers), Knockdown
    /// (hit hard in the air: lands flat, lies, gets up) and Dead.
    /// Each tick: the state machine sets Velocity, then gravity is applied and the body moves (like the player).
    /// Attack slots: an enemy needs a slot from the shared AttackTokenPool to attack (CombatSettings.maxEnemyAttackers).
    /// Everything resets when the player respawns or re-enters the enemy's room.
    /// </summary>
    [RequireComponent(typeof(KinematicBody2D))]
    public class EnemyBase : MonoBehaviour, ITickable, IHitReceiver, IParryable, IHitboxSource, IBossBarSource, IRoomReset, IGrappleTarget
    {
        [SerializeField] private EnemyData data;
        [Tooltip("Gravity and friction come from here (the player's MovementData is fine).")]
        [SerializeField] private MovementData physics;
        [SerializeField] private CombatSettings settings;
        [SerializeField] private StickFigureRig rig;
        [SerializeField] private PoseAnimator animator;

        private static readonly List<EnemyBase> all = new List<EnemyBase>();
        private static readonly Color FlashA = new Color(0.85f, 0.1f, 0.1f);
        private static readonly Color FlashB = new Color(0.45f, 0.05f, 0.05f);

        /// <summary>Shared attack slots for every enemy in the scene.</summary>
        public static readonly AttackTokenPool Tokens = new AttackTokenPool();
        public static IReadOnlyList<EnemyBase> All => all;

        private EnemyStateMachine machine;
        private EnemyAttackRunner runner;
        private Hurtbox hurtbox;
        private PlayerController target;
        private Vector2 home;
        private int hitstop, nextAttack;
        private PoseClip oneShot;
        /// <summary>Hit hard while airborne (or launched/spiked) during this hitstun: lands in a knockdown.</summary>
        private bool hardAirHit;

        // ---- States (a subclass such as FlyingEnemy may swap in its own movement states in Awake) ----
        public EnemyState Patrol { get; protected set; }
        public EnemyAlertState Alert { get; private set; }
        public EnemyState Approach { get; protected set; }
        public EnemyAttackState Attack { get; protected set; }
        public EnemyHitstunState Hitstun { get; private set; }
        public EnemyKnockdownState Knockdown { get; private set; }
        public EnemyDeadState Dead { get; private set; }

        // ---- Runtime values ----
        [System.NonSerialized] public Vector2 Velocity;
        public int Facing { get; private set; } = -1;
        public Health Health { get; private set; }
        /// <summary>Frames until this enemy may start another attack.</summary>
        public int Cooldown { get; set; }

        public int TickOrder => 22;
        public EnemyData Data => data;
        public CombatSettings Settings => settings != null ? settings : CombatSettings.Defaults;
        public KinematicBody2D Body { get; private set; }
        public Vector2 Position => Body.Position;
        public Vector2 Home => home;
        public bool Grounded => Body.Collisions.Grounded;
        public EnemyState CurrentState => machine?.Current;
        public int FramesInState => machine.FramesInState;
        public EnemyAttackRunner Runner => runner;
        public StickFigureRig Rig => rig;
        public PlayerController Target => target;
        public bool IsDead => CurrentState == Dead;
        /// <summary>Fighting the player: noticed them and not patrolling, stunned or dead. Counts toward attack slots.</summary>
        public bool IsEngaged => CurrentState == Alert || CurrentState == Approach || CurrentState == Attack;
        public int HitstopRemaining => hitstop;
        /// <summary>Flying enemies ignore ledges, and hitstun ends mid-air.</summary>
        public virtual bool Flies => false;
        /// <summary>Whether gravity pulls on the enemy this tick. Flyers only fall while stunned or dead.</summary>
        protected virtual bool GravityApplies => true;

        // IHitboxSource
        public Faction Faction => Faction.Enemy;
        public IReadOnlyList<AabbBox> ActiveHitboxes => runner.ActiveHitboxes;

        // IHitReceiver
        public bool CanBeHit => machine != null && !IsDead && !(CurrentState == Knockdown && Settings.knockdownInvulnerable);
        /// <summary>Will be knocked down on landing (set by hard hits in the air, cleared after the knockdown).</summary>
        public bool HardAirHit
        {
            get => hardAirHit;
            set => hardAirHit = value;
        }

        protected virtual void Awake()
        {
            Body = GetComponent<KinematicBody2D>();
            hurtbox = GetComponent<Hurtbox>();
            home = transform.position;
            if (rig != null) Facing = rig.transform.localScale.x < 0f ? -1 : 1;

            runner = new EnemyAttackRunner(OnAttackStarted);
            Patrol = new EnemyPatrolState(this);
            Alert = new EnemyAlertState(this);
            Approach = new EnemyApproachState(this);
            Attack = new EnemyAttackState(this);
            Hitstun = new EnemyHitstunState(this);
            Knockdown = new EnemyKnockdownState(this);
            Dead = new EnemyDeadState(this);
        }

        protected virtual void OnEnable()
        {
            all.Add(this);
            GameLoop.Register(this);
            HitboxSources.Register(this);
            BossBars.Register(this);
            GrappleTargets.Register(this);
            PlayerEvents.Respawned += OnPlayerRespawned;
        }

        protected virtual void OnDisable()
        {
            all.Remove(this);
            Tokens.Release(this);
            GameLoop.Unregister(this);
            HitboxSources.Unregister(this);
            BossBars.Unregister(this);
            GrappleTargets.Unregister(this);
            PlayerEvents.Respawned -= OnPlayerRespawned;
        }

        /// <summary>Sets dependencies from code (tests, the editor builder). Overrides the Inspector values.</summary>
        public void Configure(EnemyData enemyData, MovementData physicsData, CombatSettings combatSettings,
                              StickFigureRig figure = null, PoseAnimator poseAnimator = null)
        {
            data = enemyData;
            physics = physicsData;
            settings = combatSettings;
            rig = figure;
            animator = poseAnimator;
            machine = null;   // restart with the new data on the next tick
        }

        /// <summary>For tests: fight a specific player instead of the first one in the scene.</summary>
        public void SetTarget(PlayerController player) => target = player;

        // ---------------- tick ----------------

        public void Tick()
        {
            if (data == null || physics == null) return;
            if (machine == null) Begin();

            // Hitstop (spec 6.4): frozen after being hit (shaking) or after landing a hit.
            if (hitstop > 0)
            {
                hitstop--;
                ShakeVisual(hitstop > 0);
                return;
            }
            ShakeVisual(false);
            if (runner.Hitstop > 0)
            {
                runner.Hitstop--;
                return;
            }

            if (Cooldown > 0 && CurrentState != Attack) Cooldown--;
            FindTarget();
            machine.Tick();
            MoveBody();
            AfterMove();
            CheckFellOut();
            UpdateVisual();
        }

        /// <summary>Fell out of its room (a pit, or a tile erased under it): defeated, until the room resets.</summary>
        private void CheckFellOut()
        {
            if (IsDead) return;
            if (room == null) room = Level.Room.Of(this);
            if (room != null && Position.y < room.WorldBounds.yMin - 3f) machine.ForceState(Dead);
        }

        private Level.Room room;

        /// <summary>Every tick after the body moved (e.g. the Eraser Crawler erasing the tile it stands on).</summary>
        protected virtual void AfterMove() { }

        private void Begin()
        {
            Health = new Health(data.maxHealth);
            machine = new EnemyStateMachine();
            machine.ForceState(Patrol);
        }

        private void MoveBody()
        {
            bool grounded = Body.Collisions.Grounded;
            float dy = Velocity.y * GameTime.TickDelta;
            if (GravityApplies && (!grounded || Velocity.y > 0f))
            {
                // Juggle: airborne enemies in hitstun fall slower so air combos can keep them up.
                bool juggled = CurrentState == Hitstun || CurrentState == Dead || CurrentState == Knockdown;
                float gravity = physics.FallGravity * (juggled ? Settings.juggleGravityScale : 1f);
                // A spiked enemy (slammed downward) may fall faster than the normal max fall speed.
                float maxFall = Mathf.Max(physics.maxFallSpeed, -Velocity.y);
                Velocity.y = MovementMath.VerticalStep(Velocity.y, gravity, maxFall, out dy);
            }

            float fallSpeed = -Velocity.y;
            Body.Move(new Vector2(Velocity.x * GameTime.TickDelta, dy));
            CollisionState c = Body.Collisions;
            if (c.JustLanded && fallSpeed >= Settings.slamImpactSpeed) OnSlamImpact();
            if (c.HitWallLeft || c.HitWallRight) Velocity.x = 0f;
            if (c.HitCeiling && Velocity.y > 0f) Velocity.y = 0f;
            if (c.Grounded && Velocity.y < 0f) Velocity.y = 0f;
        }

        // ---------------- helpers used by states ----------------

        /// <summary>Accelerates toward a walking velocity (dir -1, 0 or +1), stopping at ledges and walls.</summary>
        public void Walk(int dir)
        {
            if (dir != 0 && (!GroundAhead(dir) || Body.IsTouchingWall(dir))) dir = 0;
            float step = data.walkSpeed / data.accelerationFrames;
            Velocity.x = MovementMath.Approach(Velocity.x, dir * data.walkSpeed, step);
        }

        /// <summary>Stands (or hovers) still. Used while alert.</summary>
        public virtual void Hold() => Walk(0);

        /// <summary>Slows to a stop on the ground (knockback slide).</summary>
        public void Brake(float scale = 1f)
        {
            if (Grounded && Velocity.y <= 0f)
                Velocity.x = MovementMath.Approach(Velocity.x, 0f, physics.GroundDecelStep * scale);
        }

        /// <summary>True if there is floor just past the front edge, so walking won't step off a ledge.</summary>
        public bool GroundAhead(int dir)
        {
            if (!Grounded) return true;
            Vector2 half = Body.Size * 0.5f;
            var from = new Vector2(Position.x + dir * (half.x + 0.1f), Position.y - half.y + 0.05f);
            int mask = Body.Data != null ? Body.Data.solidMask | Body.Data.oneWayMask : Physics2D.DefaultRaycastLayers;
            return Physics2D.Raycast(from, Vector2.down, 0.6f, mask).collider != null;
        }

        public void SetFacing(int direction)
        {
            if (direction == 0) return;
            int next = direction < 0 ? -1 : 1;
            // Turning around mid-walk plays the turn (visual only).
            if (next != Facing && data != null && Mathf.Abs(Velocity.x) > 0.3f && (CurrentState == Patrol || CurrentState == Approach))
                PlayOneShot(data.turn);
            Facing = next;
        }

        public void FacePlayer()
        {
            if (target != null) SetFacing(target.Body.Position.x >= Position.x ? 1 : -1);
        }

        /// <summary>Horizontal and vertical distance to the player (x is signed: + = player to the right).</summary>
        public Vector2 ToPlayer => target != null ? target.Body.Position - Position : new Vector2(float.MaxValue, 0f);

        /// <summary>A living player within notice range.</summary>
        public bool CanNoticePlayer()
        {
            if (!PlayerFightable()) return false;
            Vector2 d = ToPlayer;
            return Mathf.Abs(d.x) <= data.noticeRange && Mathf.Abs(d.y) <= data.noticeHeight;
        }

        public bool PlayerFightable() => target != null && !(target.CurrentState is DefeatedState);

        /// <summary>Another living enemy close in front (toward dir), so this one waits behind it.</summary>
        public bool BlockedByAlly(int dir)
        {
            foreach (EnemyBase other in all)
            {
                if (other == this || other.IsDead || other.machine == null) continue;
                float dx = other.Position.x - Position.x;
                if (Mathf.Sign(dx) == dir && Mathf.Abs(dx) < data.personalSpace && Mathf.Abs(other.Position.y - Position.y) < 1f)
                    return true;
            }
            return false;
        }

        /// <summary>Tries to take an attack slot. The number of slots depends on how many enemies are fighting.</summary>
        public bool TryTakeAttackSlot()
        {
            int engaged = 0;
            foreach (EnemyBase e in all) if (e.IsEngaged) engaged++;
            return Tokens.TryTake(this, AttackTokenPool.Capacity(engaged, Settings.maxEnemyAttackers));
        }

        public void ReleaseAttackSlot() => Tokens.Release(this);

        /// <summary>The next opener in EnemyData.attacks (they're used in order).</summary>
        public AttackData NextOpener()
        {
            if (data.attacks.Count == 0) return null;
            AttackData a = data.attacks[nextAttack % data.attacks.Count];
            nextAttack++;
            return a;
        }

        public void StartAttack(AttackData attack)
        {
            FacePlayer();
            runner.Start(attack);
        }

        /// <summary>Runs one attack tick; false when the attack (or string) is over.</summary>
        public bool TickAttack() => runner.Tick(this, Position, Facing, target, Settings);

        /// <summary>
        /// Plays a clip (restart = from the start even if it's already playing). A one-shot (turn) keeps playing
        /// over looping clips until it ends; a restart (attack, hurt, knockdown) cancels it.
        /// </summary>
        public void Play(PoseClip clip, bool restart = false)
        {
            if (animator == null || clip == null) return;
            if (restart)
            {
                oneShot = null;
                animator.Restart(clip);
                return;
            }
            if (oneShot != null) return;
            animator.Play(clip);
        }

        /// <summary>Plays a clip once over the current animation, then returns to it. Visual only.</summary>
        public void PlayOneShot(PoseClip clip)
        {
            if (animator == null || clip == null) return;
            oneShot = clip;
            animator.Restart(clip);
        }

        private void OnAttackStarted(AttackData attack)
        {
            FacePlayer();
            Play(attack.poseClip, restart: true);
        }

        // ---------------- taking hits ----------------

        public bool ReceiveHit(in HitInfo hit)
        {
            Health.TakeDamage(hit.Damage, 0);   // no invulnerability: enemies can be comboed
            runner.Cancel();
            ReleaseAttackSlot();

            // Hit hard in the air (launched, spiked, or a heavy hit while airborne): knocked down on landing.
            bool heavy = hit.HitstopFrames >= Settings.globalHitstopThreshold;
            if (Mathf.Abs(hit.Knockback.y) >= Settings.knockdownLaunchSpeed || (heavy && !Grounded)) hardAirHit = true;

            Velocity = hit.Knockback * data.knockbackTaken;
            // A heavy hit already froze the whole game, so no extra freeze here.
            hitstop = hit.GlobalHitstop ? 0 : hit.HitstopFrames;
            if (hit.Attacker != null) SetFacing(hit.Attacker.transform.position.x >= Position.x ? 1 : -1);

            if (Health.IsDepleted)
            {
                machine.ForceState(Dead);
            }
            else
            {
                Hitstun.Frames = hit.HitstunFrames;
                machine.ForceState(Hitstun);
            }
            return true;
        }

        /// <summary>Parried (spec 6.5): the attack is cancelled and the enemy staggers, open to a punish.</summary>
        public void OnParried(in HitInfo hit, int staggerFrames)
        {
            runner.Cancel();
            ReleaseAttackSlot();
            Velocity = Vector2.zero;
            Hitstun.Frames = staggerFrames;
            machine.ForceState(Hitstun);
        }

        /// <summary>Called by EnemyDeadState when the defeat pose is over: vanish in a burst of ink.</summary>
        /// <summary>Any enemy finished its defeat and burst into ink (sounds, counters).</summary>
        public static event System.Action<EnemyBase> Vanished;

        public void Vanish()
        {
            Vanished?.Invoke(this);
            if (InkSplatter.Instance != null) InkSplatter.Instance.Burst(Position, Vector2.up, 25);
            SetVisible(false);
            if (hurtbox != null) hurtbox.enabled = false;
            vanished = true;
        }

        // ---------------- boss bar (IBossBarSource) ----------------

        private bool vanished;

        public string BossName => data != null ? data.bossBarName : "";
        public float HealthFraction => Health != null ? Health.Fraction : 0f;
        public IReadOnlyList<float> PhaseMarks => data != null ? data.bossPhaseMarks : null;
        /// <summary>Only enemies with a boss bar name, from the moment they notice the player until they vanish.</summary>
        public bool ShowBossBar => data != null && !string.IsNullOrEmpty(data.bossBarName) && machine != null &&
                                   CurrentState != Patrol && !vanished;

        // ---------------- reset ----------------

        private void OnPlayerRespawned(PlayerController player) => ResetEnemy();

        // ---------------- Grapple Line (IGrappleTarget) ----------------

        public bool CanBeGrappled => data != null && data.grapplePullable && machine != null && !IsDead && !vanished &&
                                     CurrentState != Knockdown;
        public Vector2 GrapplePoint => Position;

        /// <summary>Yanked toward the player by the Grapple Line (spec 8): the attack stops and it's stunned mid-air.</summary>
        public void OnGrappled(Vector2 pullVelocity, int stunFrames)
        {
            runner.Cancel();
            ReleaseAttackSlot();
            Velocity = pullVelocity;
            Hitstun.Frames = Mathf.Max(1, stunFrames);
            machine.ForceState(Hitstun);
        }

        /// <summary>The player walked back into this enemy's room: it's back, fresh (spec 11.1).</summary>
        public void ResetForRoom() => ResetEnemy();

        /// <summary>Back home with full health, patrolling (on player respawn).</summary>
        public void ResetEnemy()
        {
            if (machine == null) return;
            vanished = false;
            runner.Cancel();
            ReleaseAttackSlot();
            Body.Teleport(home);
            Velocity = Vector2.zero;
            hitstop = 0;
            Cooldown = 0;
            hardAirHit = false;
            oneShot = null;
            nextAttack = 0;
            Health.Refill();
            SetVisible(true);
            if (rig != null) rig.Tint = null;
            if (hurtbox != null) hurtbox.enabled = true;
            machine.ForceState(Patrol);
        }

        // ---------------- visuals ----------------

        /// <summary>Shows or hides the enemy's drawing (hidden after the defeat animation).</summary>
        protected virtual void SetVisible(bool visible)
        {
            if (rig != null) rig.gameObject.SetActive(visible);
        }

        /// <summary>Red flash while an unparryable attack winds up (spec 6.5). Null = normal ink.</summary>
        protected Color? FlashColor()
        {
            AttackData a = runner.Current;
            bool flash = a != null && !a.parryable && runner.Frame <= a.startupFrames;
            return flash ? (runner.Frame / 3 % 2 == 0 ? FlashA : FlashB) : (Color?)null;
        }

        protected virtual void UpdateVisual()
        {
            if (rig == null) return;
            rig.transform.localScale = new Vector3(Facing, 1f, 1f);

            rig.Tint = FlashColor();

            if (animator == null) return;
            if (oneShot != null && animator.CurrentClip == oneShot && animator.Finished) oneShot = null;
            PoseClip current = animator.CurrentClip;
            bool standing = current == data.idle && oneShot == null;
            animator.SetAdditive(standing ? data.idleBreathing : null);
            // Walk cycles play in step with walking speed so the planted foot doesn't slide.
            PoseMotionSettings m = animator.Motion;
            animator.PlaybackRate = current != null && current.strideLength > 0f && current.Timeline != null
                ? CycleSync.Rate(Velocity.x, current.strideLength, current.Timeline.TotalFrames, m.minCycleRate, m.maxCycleRate)
                : 1f;
            animator.Tick();
        }

        protected virtual void ShakeVisual(bool shaking)
        {
            if (rig == null) return;
            // Alternate left/right each tick during hitstop (spec 6.4: target shakes 0.05 units).
            float x = shaking ? (hitstop % 2 == 0 ? 1f : -1f) * Settings.hitShakeDistance : 0f;
            rig.transform.localPosition = new Vector3(x, 0f, 0f);
        }

        private void OnSlamImpact()
        {
            if (CurrentState == Hitstun) hardAirHit = true;   // spiked into the floor: knocked flat
            CameraShake.Shake(0.2f);
            Vector2 feet = Position + new Vector2(0f, -Body.Size.y * 0.5f);
            if (InkSplatter.Instance != null) InkSplatter.Instance.Burst(feet, Vector2.up, 20);
        }

        private void FindTarget()
        {
            if (target == null) target = SceneQuery.FindFirst<PlayerController>();
        }
    }
}
