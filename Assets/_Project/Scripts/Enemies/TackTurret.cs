using Margin.Combat;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Enemies
{
    /// <summary>
    /// The Tack Turret (spec 9: stationary, fires projectiles, teaches dashing through). A pushpin stuck in the
    /// page (floor, wall or ceiling: <see cref="mountNormal"/>). When the player is in range and in clear view it aims,
    /// pulls its pin back and flashes red through the opener's startup (the 12+ frame telegraph), then fires a tack
    /// from its AttackData (projectileSpeed, lifetime; unparryable by default, so dash through it). It never moves
    /// and isn't knocked around; hits, death and resets work like any enemy (EnemyBase).
    /// </summary>
    public sealed class TackTurret : EnemyBase
    {
        [Tooltip("Which way the surface it's stuck to faces: (0, 1) on a floor, (0, -1) on a ceiling, (1, 0) on a left wall.")]
        [SerializeField] private Vector2 mountNormal = Vector2.up;
        [SerializeField] private TackTurretVisual visual;

        private Vector2 aim = Vector2.right;

        public override bool Flies => true;               // hitstun ends where it is, no ledge checks
        protected override bool GravityApplies => false;  // stuck to the page
        public Vector2 Aim => aim;
        public Vector2 MountNormal => mountNormal;
        public TackTurretVisual Visual => visual;
        public Vector2 Muzzle => Position + aim * 0.45f;

        public void ConfigureTurret(Vector2 normal, TackTurretVisual look)
        {
            mountNormal = normal.sqrMagnitude > 0f ? normal.normalized : Vector2.up;
            visual = look;
        }

        protected override void Awake()
        {
            base.Awake();
            aim = mountNormal.sqrMagnitude > 0f ? mountNormal.normalized : Vector2.up;
            Patrol = new TurretWatchState(this);
            Approach = new TurretAimState(this);
            Attack = new TurretFireState(this);
        }

        /// <summary>Turns the pin toward the player (it can't aim back into its own wall).</summary>
        public void AimAtPlayer(float maxDegreesPerTick = 12f)
        {
            if (Target == null) return;
            Vector2 to = (Target.Body.Position - Position).normalized;
            if (Vector2.Dot(to, mountNormal) < -0.1f) return;
            float current = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            float wanted = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            float a = Mathf.MoveTowardsAngle(current, wanted, maxDegreesPerTick) * Mathf.Deg2Rad;
            aim = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            SetFacing(aim.x >= 0f ? 1 : -1);
        }

        /// <summary>The player is in range, in front of the mount and not behind a wall.</summary>
        public bool HasClearShot()
        {
            if (!PlayerFightable()) return false;
            Vector2 to = Target.Body.Position - Position;
            if (to.magnitude > Data.noticeRange || Vector2.Dot(to, mountNormal) < -0.1f) return false;
            LayerMask solid = Body.Data != null ? Body.Data.solidMask : (LayerMask)0;
            return Physics2D.Linecast(Muzzle, Target.Body.Position, solid).collider == null;
        }

        public void Fire(AttackData attack)
        {
            LayerMask solid = Body.Data != null ? Body.Data.solidMask : (LayerMask)0;
            float speed = attack.projectileSpeed > 0f ? attack.projectileSpeed : 12f;
            EnemyProjectile.Spawn(this, attack, Settings, Muzzle, aim * speed, solid, attack.projectileLifetimeFrames);
        }

        protected override void SetVisible(bool shown)
        {
            if (visual != null) visual.gameObject.SetActive(shown);
        }

        protected override void ShakeVisual(bool shaking)
        {
            if (visual == null) return;
            float x = shaking ? (HitstopRemaining % 2 == 0 ? 1f : -1f) * Settings.hitShakeDistance : 0f;
            visual.transform.localPosition = new Vector3(x, 0f, 0f);
        }

        protected override void UpdateVisual()
        {
            if (visual == null) return;
            // Pin pulled back while winding up (the telegraph), then snapping forward.
            float pull = 0f;
            AttackData a = Runner.Current;
            if (a != null && Runner.Frame <= a.startupFrames) pull = Runner.Frame / (float)Mathf.Max(1, a.startupFrames);
            visual.Pose(mountNormal, aim, pull, FlashColor(), IsDead);
        }
    }

    /// <summary>Waits, slowly turning toward the player, until it has a clear shot.</summary>
    public sealed class TurretWatchState : EnemyState
    {
        public TurretWatchState(EnemyBase enemy) : base(enemy) { }
        private TackTurret Turret => (TackTurret)Enemy;

        public override EnemyState CheckTransitions() => Turret.HasClearShot() ? Enemy.Alert : null;

        public override void Tick()
        {
            Enemy.Velocity = Vector2.zero;
            Turret.AimAtPlayer(3f);
        }
    }

    /// <summary>Tracks the player and fires when it has a slot, no cooldown and a clear shot.</summary>
    public sealed class TurretAimState : EnemyState
    {
        public TurretAimState(EnemyBase enemy) : base(enemy) { }
        private TackTurret Turret => (TackTurret)Enemy;

        public override EnemyState CheckTransitions()
        {
            if (!Enemy.PlayerFightable()) return Enemy.Patrol;
            if (!Turret.HasClearShot()) return FramesInState > 60 ? Enemy.Patrol : null;
            if (Enemy.Cooldown <= 0 && Data.attacks.Count > 0 && Enemy.TryTakeAttackSlot()) return Enemy.Attack;
            return null;
        }

        private int FramesInState => Enemy.FramesInState;

        public override void Tick()
        {
            Enemy.Velocity = Vector2.zero;
            Turret.AimAtPlayer();
        }
    }

    /// <summary>Winds up (aim locks in the last 8 startup frames, so the shot can be read), fires, recovers.</summary>
    public sealed class TurretFireState : EnemyAttackState
    {
        public TurretFireState(EnemyBase enemy) : base(enemy) { }
        private TackTurret Turret => (TackTurret)Enemy;

        protected override void MoveDuringAttack(AttackData a, int frame)
        {
            Enemy.Velocity = Vector2.zero;
            if (a == null) return;
            if (frame <= a.startupFrames - 8) Turret.AimAtPlayer();
            if (frame == a.Timing.FirstActiveFrame) Turret.Fire(a);
        }
    }
}
