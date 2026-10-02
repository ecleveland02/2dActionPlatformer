using Margin.Level;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Enemies
{
    /// <summary>
    /// The Eraser Crawler (spec 9: deletes platform tiles it walks over; pressure and routing). A pink eraser on
    /// little legs that patrols and chases like a grunt (EnemyData: walk, notice, a short nibble attack), and rubs
    /// out any ErasableTile it stands on, cutting off routes behind it. Drawn by an EraserVisual (no stick rig).
    /// </summary>
    public sealed class EraserCrawler : EnemyBase
    {
        [SerializeField] private EraserVisual visual;

        public EraserVisual Visual => visual;

        public void ConfigureVisual(EraserVisual look) => visual = look;

        protected override void AfterMove()
        {
            if (IsDead || !Grounded) return;
            Collider2D ground = Body.GroundCollider;
            if (ground != null && ground.TryGetComponent(out ErasableTile tile)) tile.Touch();
        }

        protected override void SetVisible(bool shown)
        {
            if (visual != null) visual.gameObject.SetActive(shown);
        }

        protected override void ShakeVisual(bool shaking)
        {
            if (visual == null) return;
            float x = shaking ? (HitstopRemaining % 2 == 0 ? 1f : -1f) * Settings.hitShakeDistance : 0f;
            visual.transform.localPosition = new Vector3(x, -Body.Size.y * 0.5f, 0f);
        }

        protected override void UpdateVisual()
        {
            if (visual == null) return;
            visual.transform.localScale = new Vector3(Facing, 1f, 1f);
            var a = Runner.Current;
            float lunge = a != null && Runner.Frame > a.startupFrames - 6 && Runner.Frame <= a.Timing.LastActiveFrame ? 1f : 0f;
            visual.Pose(Mathf.Abs(Velocity.x), lunge, FlashColor(), IsDead, CurrentState == Hitstun);
        }
    }
}
