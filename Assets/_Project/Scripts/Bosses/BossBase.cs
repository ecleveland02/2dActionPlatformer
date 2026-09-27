using System.Collections.Generic;
using Margin.Combat;
using Margin.Core;
using Margin.Physics;
using Margin.Player;
using Margin.UI;
using UnityEngine;

namespace Margin.Bosses
{
    public enum BossMode { Dormant, Intro, Rest, Attacking, Staggered, PhaseShift, Defeated, Gone }

    /// <summary>
    /// Shared boss engine (spec 10). A boss is driven by a BossData asset with a list of BossPhase assets:
    ///
    ///   Dormant    waiting for the fight (BossArena calls Engage when the player walks in)
    ///   Intro      the entrance, full length the first time and short on retries; can't be hurt
    ///   Rest       between attacks (the opening): the subclass walks or hovers
    ///   Attacking  one move from the phase's pool (weighted, range-checked, never 3 in a row), run frame by
    ///              frame by the subclass from the move's AttackData (startup = the telegraph)
    ///   Staggered  its attack was parried: a long punish window
    ///   PhaseShift health reached the next phase's threshold: the phase-change sequence, can't be hurt
    ///   Defeated   the defeat animation, then Gone (Beaten fires)
    ///
    /// Bosses have super armor: hits deal damage and flash but don't knock them around; only a parry staggers.
    /// Everything resets when the player respawns or re-enters the room, unless the boss is beaten.
    /// Subclasses implement the moves (TickMove) and the look; see HighlighterBoss.
    /// </summary>
    [RequireComponent(typeof(KinematicBody2D))]
    public abstract class BossBase : MonoBehaviour, ITickable, IHitReceiver, IParryable, IHitboxSource, IBossBarSource, IRoomReset
    {
        [SerializeField] protected BossData data;
        [SerializeField] protected CombatSettings settings;
        [Tooltip("Gravity comes from here (the player's MovementData is fine).")]
        [SerializeField] protected MovementData physics;

        // Intros and phase changes seen this session: after the first view they play short (spec 10).
        private static readonly HashSet<string> seen = new HashSet<string>();

        private readonly BossMovePicker picker = new BossMovePicker(12345u);
        private readonly List<MoveOption> options = new List<MoveOption>();
        private readonly List<float> thresholds = new List<float>();
        private readonly HashSet<IHitReceiver> hitThisMove = new HashSet<IHitReceiver>();
        /// <summary>This tick's attack hitboxes (for the F1 view). Cleared at the start of every tick.</summary>
        protected readonly List<AabbBox> hitboxes = new List<AabbBox>();
        private Hurtbox hurtbox;
        private Vector2 home;
        private int homeFacing = -1;
        private int restLeft, hitstop, flashFrames;
        private float[] phaseMarks;

        [System.NonSerialized] public Vector2 Velocity;

        public BossMode Mode { get; private set; }
        /// <summary>Ticks spent in the current mode (1 on its first tick).</summary>
        public int ModeFrames { get; private set; }
        /// <summary>How long the current timed mode lasts (intro, stagger, phase shift, defeat).</summary>
        public int ModeLength { get; private set; }
        public int PhaseIndex { get; private set; }
        public Health Health { get; private set; }
        public KinematicBody2D Body { get; private set; }
        public PlayerController Target { get; private set; }
        public int Facing { get; protected set; } = -1;
        public BossMoveEntry CurrentMove { get; private set; }
        /// <summary>Frame of the current move: 1 on its first tick.</summary>
        public int MoveFrame { get; private set; }

        public BossData Data => data;
        public CombatSettings Settings => settings != null ? settings : CombatSettings.Defaults;
        public BossPhase Phase => data != null && data.phases.Count > 0 ? data.phases[Mathf.Clamp(PhaseIndex, 0, data.phases.Count - 1)] : null;
        public Vector2 Position => Body.Position;
        public Vector2 Home => home;
        /// <summary>The fight is on (from the intro until it vanishes).</summary>
        public bool Engaged => Mode != BossMode.Dormant && Mode != BossMode.Gone;
        public bool IsBeaten => Mode == BossMode.Defeated || Mode == BossMode.Gone;
        public int TickOrder => 22;

        /// <summary>Raised once the defeat animation is over and the boss has vanished.</summary>
        public event System.Action<BossBase> Beaten;

        /// <summary>A hit landed on the boss this tick or recently (for a white flash).</summary>
        protected bool HurtFlash => flashFrames > 0;

        /// <summary>An unparryable move is winding up: flash red (spec 6.5).</summary>
        protected bool TelegraphFlash =>
            Mode == BossMode.Attacking && CurrentMove.attack != null && !CurrentMove.attack.parryable &&
            MoveFrame <= CurrentMove.attack.startupFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => seen.Clear();

        /// <summary>Forget which intros and phase changes were seen (a new game, tests).</summary>
        public static void ForgetSeen() => seen.Clear();

        protected virtual void Awake()
        {
            Body = GetComponent<KinematicBody2D>();
            hurtbox = GetComponent<Hurtbox>();
            home = transform.position;
            homeFacing = Facing;
            Health = new Health(data != null ? data.maxHealth : 1);
        }

        protected virtual void OnEnable()
        {
            GameLoop.Register(this);
            HitboxSources.Register(this);
            BossBars.Register(this);
            PlayerEvents.Respawned += OnPlayerRespawned;
        }

        protected virtual void OnDisable()
        {
            GameLoop.Unregister(this);
            HitboxSources.Unregister(this);
            BossBars.Unregister(this);
            PlayerEvents.Respawned -= OnPlayerRespawned;
        }

        /// <summary>Sets dependencies from code (tests, the level builder).</summary>
        public void Configure(BossData bossData, CombatSettings combatSettings, MovementData physicsData)
        {
            data = bossData;
            settings = combatSettings;
            physics = physicsData;
            Health = new Health(data != null ? data.maxHealth : 1);
        }

        public void SetTarget(PlayerController player) => Target = player;

        // ---------------- the fight ----------------

        /// <summary>Starts the fight (the arena calls this). The entrance is short if it was seen before.</summary>
        public void Engage()
        {
            if (Mode != BossMode.Dormant || data == null) return;
            if (Target == null) Target = SceneQuery.FindFirst<PlayerController>();
            string key = name + "/intro";
            bool repeat = seen.Contains(key);
            seen.Add(key);
            Enter(BossMode.Intro, repeat ? data.introFramesRepeat : data.introFrames);
            OnIntroStart(repeat);
            BossEvents.RaiseEngaged(this);
        }

        public void Tick()
        {
            if (data == null) return;
            if (flashFrames > 0) flashFrames--;
            if (hitstop > 0)
            {
                hitstop--;
                UpdateVisual();
                return;
            }

            ModeFrames++;
            hitboxes.Clear();
            switch (Mode)
            {
                case BossMode.Dormant:
                    OnDormantTick();
                    break;
                case BossMode.Intro:
                    OnIntroTick();
                    if (ModeFrames >= ModeLength) EnterRest();
                    break;
                case BossMode.Rest:
                    FacePlayer();
                    OnRestTick();
                    if (--restLeft <= 0) StartNextMove();
                    break;
                case BossMode.Attacking:
                    MoveFrame++;
                    if (!TickMove(CurrentMove, MoveFrame))
                    {
                        OnMoveEnd(CurrentMove);
                        EnterRest();
                    }
                    break;
                case BossMode.Staggered:
                    OnStaggerTick();
                    if (ModeFrames >= ModeLength) EnterRest();
                    break;
                case BossMode.PhaseShift:
                    OnPhaseShiftTick();
                    if (ModeFrames >= ModeLength) EnterRest();
                    break;
                case BossMode.Defeated:
                    OnDefeatTick();
                    if (ModeFrames >= ModeLength)
                    {
                        Enter(BossMode.Gone, 0);
                        SetHurtbox(false);
                        OnVanish();
                        Beaten?.Invoke(this);
                        BossEvents.RaiseBeaten(this);
                    }
                    break;
            }
            OnAnyTick();
            UpdateVisual();
        }

        private void Enter(BossMode mode, int length)
        {
            Mode = mode;
            ModeFrames = 0;
            ModeLength = length;
        }

        private void EnterRest()
        {
            Enter(BossMode.Rest, 0);
            BossPhase p = Phase;
            restLeft = p != null ? picker.Range(p.restFramesMin, p.restFramesMax) : 30;
            OnRestStart();
        }

        private void StartNextMove()
        {
            BossPhase p = Phase;
            if (p == null || p.moves.Count == 0)
            {
                EnterRest();
                return;
            }
            options.Clear();
            foreach (BossMoveEntry m in p.moves)
                options.Add(new MoveOption(m.attack != null && CanUse(m) ? m.weight : 0, m.minRange, m.maxRange));
            int pick = picker.Pick(options, DistanceToTarget);
            if (pick < 0)
            {
                EnterRest();
                return;
            }
            StartMove(p.moves[pick]);
        }

        /// <summary>Starts a specific move now (the pool normally picks; tests call this directly).</summary>
        public void StartMove(BossMoveEntry move)
        {
            CurrentMove = move;
            MoveFrame = 0;
            hitThisMove.Clear();
            Enter(BossMode.Attacking, 0);
            FacePlayer();
            OnMoveStart(move);
            BossEvents.RaiseMoveStarted(this, TellSound(move));
        }

        // ---------------- taking hits ----------------

        public bool CanBeHit =>
            (Mode == BossMode.Rest || Mode == BossMode.Attacking || Mode == BossMode.Staggered) && !Health.IsDepleted;

        public bool ReceiveHit(in HitInfo hit)
        {
            TakeDamage(hit.Damage);
            if (!hit.GlobalHitstop) hitstop = Mathf.Max(hitstop, data.hitstopOnHurt);
            OnHurt(hit);
            return true;
        }

        /// <summary>Damage from something other than a sword hit (e.g. its own cap parried back at it).</summary>
        public void TakeDamage(int amount)
        {
            if (!CanBeHit) return;
            Health.TakeDamage(amount, 0);
            flashFrames = 6;
            if (Health.IsDepleted)
            {
                EnterDefeated();
                return;
            }
            int next = BossPhases.PhaseFor(Health.Fraction, Thresholds());
            if (next > PhaseIndex) EnterPhaseShift(next);
        }

        /// <summary>Parried (spec 6.5): the move stops and the boss staggers, wide open.</summary>
        public void OnParried(in HitInfo hit, int staggerFrames) => Stagger(data.parryStaggerFrames);

        public void Stagger(int frames)
        {
            if (Mode != BossMode.Attacking && Mode != BossMode.Rest && Mode != BossMode.Staggered) return;
            if (Mode == BossMode.Attacking) OnMoveEnd(CurrentMove);
            Enter(BossMode.Staggered, frames);
            OnStaggerStart();
        }

        private void EnterPhaseShift(int phase)
        {
            if (Mode == BossMode.Attacking) OnMoveEnd(CurrentMove);
            PhaseIndex = phase;
            string key = name + "/phase" + phase;
            bool repeat = seen.Contains(key);
            seen.Add(key);
            BossPhase p = Phase;
            Enter(BossMode.PhaseShift, p == null ? 0 : repeat ? p.transitionFramesRepeat : p.transitionFrames);
            OnPhaseShiftStart(phase, repeat);
            BossEvents.RaisePhaseChanged(this, phase);
        }

        private void EnterDefeated()
        {
            if (Mode == BossMode.Attacking) OnMoveEnd(CurrentMove);
            Enter(BossMode.Defeated, data.defeatFrames);
            OnDefeatStart();
        }

        private List<float> Thresholds()
        {
            thresholds.Clear();
            foreach (BossPhase p in data.phases) thresholds.Add(p != null ? p.healthThreshold : 1f);
            return thresholds;
        }

        // ---------------- reset ----------------

        private void OnPlayerRespawned(PlayerController player)
        {
            if (!IsBeaten) ResetBoss();
        }

        public void ResetForRoom()
        {
            if (!IsBeaten) ResetBoss();
        }

        /// <summary>Back to the start, asleep, full health, phase 1.</summary>
        public void ResetBoss()
        {
            if (Mode == BossMode.Attacking) OnMoveEnd(CurrentMove);
            Enter(BossMode.Dormant, 0);
            PhaseIndex = 0;
            Health = new Health(data != null ? data.maxHealth : 1);
            Body.Teleport(home);
            Velocity = Vector2.zero;
            Facing = homeFacing;
            hitstop = 0;
            flashFrames = 0;
            hitboxes.Clear();
            picker.Reseed((uint)System.Environment.TickCount);
            SetHurtbox(true);
            OnReset();
            BossEvents.RaiseReset(this);
            UpdateVisual();
        }

        // ---------------- helpers for subclasses ----------------

        /// <summary>Horizontal distance to the player (units).</summary>
        public float DistanceToTarget => Target != null ? Mathf.Abs(Target.Body.Position.x - Position.x) : 999f;

        public void FacePlayer()
        {
            if (Target == null) return;
            float dx = Target.Body.Position.x - Position.x;
            if (Mathf.Abs(dx) > 0.2f) Facing = dx > 0f ? 1 : -1;
        }

        /// <summary>Tests one attack hitbox this tick. Each target is hit at most once per move.</summary>
        protected int Strike(AttackData attack, AabbBox box)
        {
            hitboxes.Add(box);
            int hits = HitResolver.Resolve(this, Faction.Enemy, attack, box, Facing, hitThisMove, Settings);
            if (hits > 0 && attack.hitstopFrames < Settings.globalHitstopThreshold) hitstop = Mathf.Max(hitstop, attack.hitstopFrames);
            return hits;
        }

        /// <summary>Walks/falls with gravity and level collision (like the player).</summary>
        protected void MoveGrounded()
        {
            bool grounded = Body.Collisions.Grounded;
            float dy = Velocity.y * GameTime.TickDelta;
            if (!grounded || Velocity.y > 0f)
            {
                float gravity = physics != null ? physics.FallGravity : 60f;
                float maxFall = physics != null ? physics.maxFallSpeed : 20f;
                Velocity.y = MovementMath.VerticalStep(Velocity.y, gravity, maxFall, out dy);
            }
            Body.Move(new Vector2(Velocity.x * GameTime.TickDelta, dy));
            CollisionState c = Body.Collisions;
            if (c.HitWallLeft || c.HitWallRight) Velocity.x = 0f;
            if (c.HitCeiling && Velocity.y > 0f) Velocity.y = 0f;
            if (c.Grounded && Velocity.y < 0f) Velocity.y = 0f;
        }

        /// <summary>Flies: moves by Velocity ignoring the level.</summary>
        protected void MoveFlying() => Body.MoveFree(Velocity * GameTime.TickDelta);

        /// <summary>Sets the hurtbox (e.g. lying flat for a dash). Size is centered on the body.</summary>
        protected void SetHurtboxShape(Vector2 offset, Vector2 size)
        {
            if (hurtbox != null) hurtbox.Configure(Faction.Enemy, offset, size);
        }

        private void SetHurtbox(bool on)
        {
            if (hurtbox != null) hurtbox.enabled = on;
        }

        // ---------------- IHitboxSource / IBossBarSource ----------------

        public Faction Faction => Faction.Enemy;
        public IReadOnlyList<AabbBox> ActiveHitboxes => hitboxes;

        public string BossName => data != null ? data.bossName : "";
        public float HealthFraction => Health != null ? Health.Fraction : 0f;

        public IReadOnlyList<float> PhaseMarks
        {
            get
            {
                if (phaseMarks == null && data != null)
                {
                    var marks = new List<float>();
                    for (int i = 1; i < data.phases.Count; i++)
                        if (data.phases[i] != null) marks.Add(data.phases[i].healthThreshold);
                    phaseMarks = marks.ToArray();
                }
                return phaseMarks;
            }
        }

        /// <summary>Spec 10: the health bar is always visible during the fight.</summary>
        public bool ShowBossBar => Engaged;

        // ---------------- hooks ----------------

        /// <summary>The move's telegraph sound (spec 10: every attack has a consistent sound). Empty = none.</summary>
        protected virtual string TellSound(BossMoveEntry move) => move.tellSound;

        /// <summary>Whether a move may be picked right now (e.g. not tossing a cap that's already flying).</summary>
        protected virtual bool CanUse(BossMoveEntry move) => true;
        protected virtual void OnDormantTick() { }
        protected virtual void OnIntroStart(bool repeat) { }
        protected virtual void OnIntroTick() { }
        protected virtual void OnRestStart() { }
        protected virtual void OnRestTick() { }
        protected abstract void OnMoveStart(BossMoveEntry move);
        /// <summary>Runs one frame of the move. Return false when it's finished.</summary>
        protected abstract bool TickMove(BossMoveEntry move, int frame);
        /// <summary>The move finished, was parried or interrupted: tidy up.</summary>
        protected virtual void OnMoveEnd(BossMoveEntry move) { }
        protected virtual void OnStaggerStart() { }
        protected virtual void OnStaggerTick() { }
        protected virtual void OnPhaseShiftStart(int phase, bool repeat) { }
        protected virtual void OnPhaseShiftTick() { }
        protected virtual void OnDefeatStart() { }
        protected virtual void OnDefeatTick() { }
        protected virtual void OnVanish() { }
        protected virtual void OnHurt(in HitInfo hit) { }
        protected virtual void OnReset() { }
        /// <summary>Every tick after the mode's work (projectiles, lingering effects).</summary>
        protected virtual void OnAnyTick() { }
        protected abstract void UpdateVisual();
    }
}
