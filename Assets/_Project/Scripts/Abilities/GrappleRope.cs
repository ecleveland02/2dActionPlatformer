using Margin.Player;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Abilities
{
    /// <summary>
    /// Draws the Grapple Line (spec 8, "Pen Stroke"): an ink line from the player's free (back) hand to the ring
    /// while swinging, or to an enemy for a moment after yanking it. Also highlights the ring the line would hook
    /// right now, so you know before you throw. Added to the player automatically.
    /// Runs in LateUpdate after the pose is drawn, so the line starts exactly at the drawn hand.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(PlayerController))]
    public sealed class GrappleRope : MonoBehaviour
    {
        private static readonly Color Ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);

        private PlayerController player;
        private StickFigureRig rig;
        private LineRenderer line;
        private GrappleAnchor highlighted;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            var go = new GameObject("Grapple Line");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = InkMaterial.Runtime;
            line.widthMultiplier = 0.045f;
            line.startColor = line.endColor = Ink;
            line.numCapVertices = 2;
            line.sortingOrder = 9;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
        }

        private void OnDisable()
        {
            if (highlighted != null) highlighted.SetHighlight(false);
            highlighted = null;
        }

        private void LateUpdate()
        {
            if (player == null || player.CurrentState == null || player.Data == null) return;
            if (rig == null && player.VisualRoot != null) rig = player.VisualRoot.GetComponent<StickFigureRig>();
            Vector3 hand = rig != null && rig.IsBuilt ? rig.HandPosition(false) : (Vector3)player.GrappleOrigin;

            if (player.CurrentState is GrappleState swing && swing.Anchor != null) Draw(hand, swing.AnchorPoint);
            else if (player.PullLineFrames > 0) Draw(hand, player.LastPullPoint);
            else line.enabled = false;

            GrappleAnchor target = null;
            if (player.Abilities != null && player.Abilities.grappleLine && !(player.CurrentState is GrappleState) &&
                player.Body != null && player.FindGrappleTarget(out GrappleAnchor a, out IGrappleTarget _))
                target = a;
            if (target == highlighted) return;
            if (highlighted != null) highlighted.SetHighlight(false);
            if (target != null) target.SetHighlight(true);
            highlighted = target;
        }

        /// <summary>A pen stroke from hand to target: straight, with a slight hand-drawn kink in the middle.</summary>
        private void Draw(Vector3 from, Vector2 to)
        {
            line.enabled = true;
            Vector3 end = new Vector3(to.x, to.y, 0f);
            Vector3 mid = Vector3.Lerp(from, end, 0.5f);
            Vector3 dir = (end - from).normalized;
            mid += new Vector3(-dir.y, dir.x, 0f) * 0.03f;
            line.positionCount = 3;
            line.SetPosition(0, from);
            line.SetPosition(1, mid);
            line.SetPosition(2, end);
        }
    }
}
