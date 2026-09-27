using Margin.Abilities;
using Margin.Core;
using Margin.Input;
using UnityEngine;

namespace Margin.Player
{
    /// <summary>
    /// Swinging on the Grapple Line (spec 8). The line is a rope: gravity pulls the player down and, whenever the
    /// next position would be farther from the ring than the rope is long, the player is kept on the circle instead,
    /// which turns falling into a swing. Left/Right pump the swing, Up/Down reel in and out, and attaching yanks the
    /// rope a little shorter (lifting you off the ground). Let go with Jump (a small upward boost), Grapple again,
    /// or any attack, parry or dash. Letting go gives back the air dash. Touching the ground ends the swing.
    /// </summary>
    public sealed class GrappleState : PlayerState
    {
        private int yankLeft;

        public GrappleState(PlayerController player) : base(player) { }

        public GrappleAnchor Anchor { get; private set; }
        public Vector2 AnchorPoint => Anchor != null ? Anchor.Point : Player.Body.Position;
        public float RopeLength { get; private set; }

        /// <summary>The rope's angle from straight up, toward the facing direction (degrees). Tilts the pose.</summary>
        public float RopeAngle
        {
            get
            {
                Vector2 d = AnchorPoint - Player.Body.Position;
                return GrappleAim.AngleFromUp(d.x, d.y, Player.Facing);
            }
        }

        /// <summary>Called by PlayerController.CheckGrapple just before entering.</summary>
        public void Attach(GrappleAnchor anchor) => Anchor = anchor;

        public override void Enter()
        {
            float distance = Vector2.Distance(AnchorPoint, Player.Body.Position);
            RopeLength = Mathf.Clamp(distance, Data.grappleMinLength, Data.grappleRange);
            yankLeft = Data.grappleYankFrames;
        }

        public override PlayerState CheckTransitions()
        {
            if (Anchor == null || !Anchor.isActiveAndEnabled) return Player.Fall;
            IPlayerInput input = Player.Controls;
            if (input.Buffer.Consume(BufferedAction.Jump))
            {
                // Jump off: keep the swing's momentum, plus a hop.
                Player.Velocity.y = Mathf.Max(Player.Velocity.y, 0f) + Data.grappleReleaseBoost;
                return Player.Fall;
            }
            if (input.Buffer.Consume(BufferedAction.Grapple)) return Player.Fall;
            if (Player.Grounded && Player.Velocity.y <= 0f) return Player.Land;
            return Player.CheckParry() ?? Player.CheckAttack() ?? Player.CheckDash();
        }

        public override void Tick()
        {
            float dt = GameTime.TickDelta;
            Vector2 position = Player.Body.Position;
            Vector2 anchor = AnchorPoint;
            Vector2 v = Player.Velocity;

            v.y -= Data.FallGravity * Data.grappleGravityScale * dt;

            // Pump along the swing (the rope's tangent, pointed so +x is "right").
            Vector2 toAnchor = anchor - position;
            float distance = toAnchor.magnitude;
            Vector2 inward = distance > 0.0001f ? toAnchor / distance : Vector2.up;
            var tangent = new Vector2(inward.y, -inward.x);
            if (tangent.x < 0f) tangent = -tangent;
            v += tangent * (Player.InputX * Data.grappleSwingAcceleration * dt);

            // Yank on attaching, then reel with Up/Down.
            if (yankLeft > 0)
            {
                yankLeft--;
                RopeLength -= Data.grappleYank / Mathf.Max(1, Data.grappleYankFrames);
            }
            else if (Player.Controls.UpHeld) RopeLength -= Data.grappleReelSpeed * dt;
            else if (Player.Controls.DownHeld) RopeLength += Data.grappleReelSpeed * dt;
            RopeLength = Mathf.Clamp(RopeLength, Data.grappleMinLength, Data.grappleRange);

            // The rope: never end the tick farther away than its length (slide along the circle instead).
            Vector2 next = position + v * dt;
            Vector2 fromAnchor = next - anchor;
            float d = fromAnchor.magnitude;
            if (d > RopeLength && d > 0.0001f)
            {
                next = anchor + fromAnchor / d * RopeLength;
                v = (next - position) / dt;
            }
            if (v.magnitude > Data.grappleMaxSpeed) v = v.normalized * Data.grappleMaxSpeed;
            Player.Velocity = v;

            if (Player.InputX != 0) Player.Facing = Player.InputX;
            else if (Mathf.Abs(v.x) > 1f) Player.Facing = v.x > 0f ? 1 : -1;
        }

        public override void Exit()
        {
            Anchor = null;
            Player.OnGrappleReleased();
        }
    }
}
