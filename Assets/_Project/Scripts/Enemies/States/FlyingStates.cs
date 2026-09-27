using Margin.Combat;
using UnityEngine;

namespace Margin.Enemies
{
    /// <summary>Drifts slowly left and right around its home with a small bob, until it notices the player.</summary>
    public sealed class FlyPatrolState : EnemyState
    {
        private readonly FlyingEnemy bat;
        private int tick;

        public FlyPatrolState(FlyingEnemy enemy) : base(enemy) => bat = enemy;

        public override EnemyState CheckTransitions() => bat.CanNoticePlayer() ? bat.Alert : null;

        public override void Tick()
        {
            tick++;
            // One slow sweep every 4 seconds, bobbing once a second.
            float x = Data.patrolDistance * Mathf.Sin(tick * 2f * Mathf.PI / 240f);
            float y = 0.15f * Mathf.Sin(tick * 2f * Mathf.PI / 60f);
            Vector2 point = bat.Home + new Vector2(x, y);
            if (Mathf.Abs(point.x - bat.Position.x) > 0.05f) bat.SetFacing(point.x > bat.Position.x ? 1 : -1);
            bat.FlyToward(point, Data.flySpeed * 0.5f);
        }
    }

    /// <summary>
    /// Flies to a spot above and to the side of the player (whichever side it's on) and dives once it gets there
    /// with a free attack slot and no cooldown. Gives up when the player is far away or defeated.
    /// </summary>
    public sealed class FlyApproachState : EnemyState
    {
        private readonly FlyingEnemy bat;

        public FlyApproachState(FlyingEnemy enemy) : base(enemy) => bat = enemy;

        private Vector2 HoverSpot()
        {
            Vector2 player = bat.Target.Body.Position;
            float side = bat.Position.x >= player.x ? 1f : -1f;
            return player + new Vector2(side * Data.hoverSide, Data.hoverHeight);
        }

        public override EnemyState CheckTransitions()
        {
            if (!bat.PlayerFightable() || Mathf.Abs(bat.ToPlayer.x) > Data.giveUpRange) return bat.Patrol;
            bool inPosition = Vector2.Distance(bat.Position, HoverSpot()) <= 0.6f;
            if (inPosition && bat.Cooldown <= 0 && Data.attacks.Count > 0 && bat.TryTakeAttackSlot()) return bat.Attack;
            return null;
        }

        public override void Tick()
        {
            bat.FacePlayer();
            bat.FlyToward(HoverSpot(), Data.flySpeed);
        }
    }

    /// <summary>
    /// The dive. Wind-up (startup): hovers and rises slightly with wings raised. First active frame: locks onto
    /// where the player is and dives in a straight line at diveSpeed (the hitbox is out for the whole dive, so
    /// moving or jumping during the wind-up dodges it). Recovery: flaps back up.
    /// </summary>
    public sealed class DiveAttackState : EnemyAttackState
    {
        private readonly FlyingEnemy bat;
        private Vector2 diveDirection;

        public DiveAttackState(FlyingEnemy enemy) : base(enemy) => bat = enemy;

        protected override void MoveDuringAttack(AttackData a, int frame)
        {
            if (a == null)
            {
                bat.Hold();
                return;
            }

            AttackTiming t = a.Timing;
            if (frame <= t.Startup)
            {
                bat.SteerTo(new Vector2(0f, 1f));   // pull up a little: the wind-up
                return;
            }

            if (frame <= t.LastActiveFrame)
            {
                if (frame == t.FirstActiveFrame)
                {
                    Vector2 toPlayer = bat.ToPlayer;
                    diveDirection = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.down;
                    bat.SetFacing(diveDirection.x >= 0f ? 1 : -1);
                }
                bat.Velocity = diveDirection * Data.diveSpeed;
                return;
            }

            // Recovery: brake out of the dive and climb away.
            bat.SteerTo(new Vector2(0f, Data.flySpeed * 0.6f), accelerationScale: 3f);
        }
    }
}
