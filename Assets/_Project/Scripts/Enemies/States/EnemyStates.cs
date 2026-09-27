using Margin.Combat;
using Margin.Player;
using UnityEngine;

namespace Margin.Enemies
{
    /// <summary>Walks back and forth around its home, pausing at each end, until it notices the player.</summary>
    public sealed class EnemyPatrolState : EnemyState
    {
        private int dir = 1, pause;

        public EnemyPatrolState(EnemyBase enemy) : base(enemy) { }

        public override void Enter() => pause = 0;

        public override EnemyState CheckTransitions() => Enemy.CanNoticePlayer() ? Enemy.Alert : null;

        public override void Tick()
        {
            Enemy.Play(Mathf.Abs(Enemy.Velocity.x) > 0.1f ? Data.walk : Data.idle);
            // A guard (patrol distance 0) just walks back to its spot and stands there.
            if (Data.patrolDistance <= 0f)
            {
                float toHome = Enemy.Home.x - Enemy.Position.x;
                int homeDir = Mathf.Abs(toHome) < 0.1f ? 0 : (toHome > 0f ? 1 : -1);
                Enemy.SetFacing(homeDir);
                Enemy.Walk(homeDir);
                return;
            }

            if (pause > 0)
            {
                pause--;
                Enemy.Walk(0);
                return;
            }

            // Turn at the ends of the patrol, at ledges and at walls (heading home first if knocked away).
            float offset = Enemy.Position.x - Enemy.Home.x;
            bool pastEnd = offset * dir >= Data.patrolDistance;
            bool blocked = !Enemy.GroundAhead(dir) || Enemy.Body.IsTouchingWall(dir);
            if (pastEnd || blocked)
            {
                dir = -dir;
                pause = Data.patrolPauseFrames;
                Enemy.Walk(0);
                return;
            }

            Enemy.SetFacing(dir);
            Enemy.Walk(dir);
        }
    }

    /// <summary>"Noticed you": stops, turns to the player and holds the alert pose for alertFrames.</summary>
    public sealed class EnemyAlertState : EnemyState
    {
        public EnemyAlertState(EnemyBase enemy) : base(enemy) { }

        public override void Enter()
        {
            Enemy.FacePlayer();
            Enemy.Play(Data.alert != null ? Data.alert : Data.idle, restart: true);
        }

        public override EnemyState CheckTransitions() =>
            Enemy.FramesInState >= Data.alertFrames ? Enemy.Approach : null;

        public override void Tick() => Enemy.Hold();
    }

    /// <summary>
    /// Walks toward the player and attacks when in range with a free attack slot and no cooldown.
    /// Waits behind other enemies instead of stacking on them. Gives up when the player is far away or defeated.
    /// </summary>
    public sealed class EnemyApproachState : EnemyState
    {
        public EnemyApproachState(EnemyBase enemy) : base(enemy) { }

        public override EnemyState CheckTransitions()
        {
            if (!Enemy.PlayerFightable() || Mathf.Abs(Enemy.ToPlayer.x) > Data.giveUpRange) return Enemy.Patrol;

            Vector2 d = Enemy.ToPlayer;
            float distance = Mathf.Abs(d.x);
            bool inRange = distance <= Data.attackRange && distance >= Data.minAttackRange && Mathf.Abs(d.y) <= 1f;
            if (inRange && Enemy.Cooldown <= 0 && Data.attacks.Count > 0 && Enemy.TryTakeAttackSlot())
                return Enemy.Attack;
            return null;
        }

        public override void Tick()
        {
            Enemy.FacePlayer();
            float dx = Enemy.ToPlayer.x;
            int dir = dx > 0f ? 1 : -1;
            // Too close for comfort (spear users): back away while still facing the player.
            if (Mathf.Abs(dx) < Data.retreatDistance)
            {
                Enemy.Walk(-dir);
                Enemy.Play(Mathf.Abs(Enemy.Velocity.x) > 0.1f ? Data.walk : Data.idle);
                return;
            }

            // Close in to a bit inside attack range, but don't walk into the player or through allies.
            bool close = Mathf.Abs(dx) <= Data.attackRange * 0.8f;
            Enemy.Walk(close || Enemy.BlockedByAlly(dir) ? 0 : dir);
            Enemy.Play(Mathf.Abs(Enemy.Velocity.x) > 0.1f ? Data.walk : Data.idle);
        }
    }

    /// <summary>
    /// Runs an opener and any combo follow-ups (EnemyAttackRunner). Holds an attack slot the whole time.
    /// Lunging attacks move forward from lungeFirstFrame through their active frames; otherwise it plants.
    /// </summary>
    public class EnemyAttackState : EnemyState
    {
        private bool attacking;

        public EnemyAttackState(EnemyBase enemy) : base(enemy) { }

        public override void Enter()
        {
            AttackData opener = Enemy.NextOpener();
            attacking = opener != null;
            if (attacking) Enemy.StartAttack(opener);
        }

        public override EnemyState CheckTransitions() => attacking ? null : Enemy.Approach;

        public override void Tick()
        {
            if (!attacking) return;
            attacking = Enemy.TickAttack();
            MoveDuringAttack(Enemy.Runner.Current, Enemy.Runner.Frame);
        }

        /// <summary>Called every attack tick after the runner (a is null once the string ended). Subclasses such as
        /// the bat's dive override this. Ground attacks: optional forward lunge from lungeFirstFrame through the active frames, else plant.</summary>
        protected virtual void MoveDuringAttack(AttackData a, int frame)
        {
            bool lunging = a != null && a.lungeSpeed > 0f && frame >= a.lungeFirstFrame && frame <= a.Timing.LastActiveFrame;
            if (lunging && Enemy.GroundAhead(Enemy.Facing)) Enemy.Velocity.x = Enemy.Facing * a.lungeSpeed;
            else Enemy.Brake();
        }

        public override void Exit()
        {
            Enemy.Runner.Cancel();
            Enemy.ReleaseAttackSlot();
            Enemy.Cooldown = Data.attackCooldownFrames;
            attacking = false;
        }
    }

    /// <summary>
    /// Knocked back by a hit, launched, or staggered by a parry. Can't act for Frames ticks, and stays until it
    /// lands (airborne enemies fall with juggle gravity so air combos can keep them up). Then it fights back.
    /// </summary>
    public sealed class EnemyHitstunState : EnemyState
    {
        public EnemyHitstunState(EnemyBase enemy) : base(enemy) { }

        public int Frames { get; set; }

        public override void Enter() => Enemy.Play(Data.hurt, restart: true);

        public override EnemyState CheckTransitions()
        {
            if (Enemy.FramesInState < Frames || (!Enemy.Grounded && !Enemy.Flies)) return null;
            return Enemy.PlayerFightable() ? (EnemyState)Enemy.Approach : Enemy.Patrol;
        }

        public override void Tick() => Enemy.Brake(0.5f);
    }

    /// <summary>Health reached 0: the defeat pose (the body still flies from the last hit), then it vanishes.</summary>
    public sealed class EnemyDeadState : EnemyState
    {
        public EnemyDeadState(EnemyBase enemy) : base(enemy) { }

        public override void Enter()
        {
            Enemy.Play(Data.defeated != null ? Data.defeated : Data.hurt, restart: true);
            Debug.Log($"[Enemy] {Enemy.name} defeated", Enemy);
        }

        public override void Tick()
        {
            Enemy.Brake(0.5f);
            if (Enemy.FramesInState == Data.deathFrames) Enemy.Vanish();
        }
    }
}
