using Margin.Core;
using Margin.FX;
using UnityEngine;

namespace Margin.Player
{
    /// <summary>
    /// Combo breaker: entered from hitstun by pressing Parry with enough ink (CombatSettings.comboBreakerInkCost).
    /// On its first frame an ink shockwave pushes nearby enemies away and stuns them, cancelling their attacks.
    /// The player hangs in place and can't be hit for comboBreakerFrames, then gets control back
    /// (plus the normal post-combo invulnerability from PlayerHealth).
    /// </summary>
    public sealed class ComboBreakerState : PlayerState
    {
        public ComboBreakerState(PlayerController player) : base(player) { }

        private Margin.Combat.CombatSettings Settings => Player.Combat.Settings;

        public override void Enter()
        {
            Player.Velocity = Vector2.zero;
            Player.IsInvulnerable = true;
        }

        public override void Exit() => Player.IsInvulnerable = false;

        public override PlayerState CheckTransitions()
        {
            if (Player.FramesInState < Settings.comboBreakerFrames) return null;
            return Player.Grounded ? (PlayerState)Player.Idle : Player.Fall;
        }

        public override void Tick()
        {
            if (Player.FramesInState == 1) Burst();
            // Hang in the air (an escape from air combos too), stopped on the ground.
            Player.Velocity = Vector2.zero;
        }

        private void Burst()
        {
            int pushed = Player.Combat.ComboBreakerPush();

            Vector2 at = Player.Body.Position;
            if (InkSplatter.Instance != null)
            {
                InkSplatter.Instance.Burst(at, new Vector2(1f, 0.3f), 20);
                InkSplatter.Instance.Burst(at, new Vector2(-1f, 0.3f), 20);
            }
            CameraShake.Shake(0.15f);
            if (Settings.comboBreakerHitstopFrames > 0) GameLoop.Freeze(Settings.comboBreakerHitstopFrames);
            Debug.Log($"[Player] COMBO BREAKER ({Settings.comboBreakerInkCost} ink), pushed {pushed}", Player);
        }
    }
}
