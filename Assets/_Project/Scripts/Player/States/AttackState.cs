using Margin.Combat;
using UnityEngine;

namespace Margin.Player
{
    /// <summary>
    /// Performing an attack (ground or air) from its AttackData (spec 6.2/6.3).
    /// Frame N of the attack is FramesInState = N. Hitboxes are checked after the player moves each active frame.
    /// Cancels: on hit, during the cancel window, into any move in "Cancels into", or jump/dash if allowed.
    /// On whiff, only jump/dash, and only from the window start. Facing is locked for the whole attack.
    /// </summary>
    public sealed class AttackState : PlayerState
    {
        public AttackState(PlayerController player) : base(player) { }

        public AttackData Attack { get; private set; }
        public bool HasHit { get; private set; }
        public bool Airborne { get; private set; }

        /// <summary>Attack frame this state is on (1 = first tick).</summary>
        public int Frame => Player.FramesInState;

        public override void Enter()
        {
            Attack = Player.Combat.PendingAttack;
            HasHit = false;
            Airborne = !Player.Grounded;
            Player.Combat.BeginAttack(Attack);
        }

        public override void Exit()
        {
            Player.Combat.EndAttack();
        }

        public override PlayerState CheckTransitions()
        {
            int next = Frame + 1;             // the frame about to run
            AttackTiming timing = Attack.Timing;

            if (timing.AllowsAttackCancel(next, HasHit) && Player.Combat.TryCancelInto(Attack))
                return Player.Attack;         // re-enter with the new attack

            if (timing.AllowsMovementCancel(next, HasHit))
            {
                PlayerState move = (Attack.cancelsIntoJump ? Player.CheckGroundJump() : null)
                                   ?? (Attack.cancelsIntoDash ? Player.CheckDash() : null);
                if (move != null) return move;
            }

            // Air attacks end when you land.
            if (Airborne && Player.Grounded && Player.Velocity.y <= 0f) return Player.Land;

            if (next > timing.TotalFrames)
            {
                if (!Player.Grounded) return Player.Fall;
                return Player.InputX != 0 ? (PlayerState)Player.Run : Player.Idle;
            }
            return null;
        }

        public override void Tick()
        {
            AttackTiming timing = Attack.Timing;

            if (Airborne || !Player.Grounded)
            {
                // Air attacks keep momentum and some air control, but never turn you around.
                Player.ApplyHorizontal(onGround: false, updateFacing: false);
                Player.ApplyGravity(Data.maxFallSpeed);
                return;
            }

            // Ground attacks: optional forward lunge during startup/active, otherwise brake to a stop.
            Player.Velocity.y = 0f;
            bool lunging = Attack.lungeSpeed > 0f && Frame <= timing.LastActiveFrame;
            Player.Velocity.x = lunging
                ? Player.Facing * Attack.lungeSpeed
                : MovementMath.Approach(Player.Velocity.x, 0f, Data.GroundDecelStep);
        }

        public override void PostMove()
        {
            if (Attack.Timing.IsActive(Frame) && Player.Combat.ResolveHits(Attack, Frame)) HasHit = true;
        }
    }
}
