using UnityEngine;

namespace Margin.Rendering
{
    /// <summary>
    /// Draws a held blade as an ink line from the rig's front hand, continuing the forearm's direction.
    /// Length is set from the weapon (WeaponData.bladeLength) by PlayerCombat.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class WeaponLine : MonoBehaviour
    {
        [SerializeField] private StickFigureRig rig;
        [SerializeField, Min(0f)] private float length = 0.9f;
        [SerializeField, Min(0.001f)] private float width = 0.035f;

        private LineRenderer line;

        public StickFigureRig Rig
        {
            get => rig;
            set => rig = value;
        }

        public float Length
        {
            get => length;
            set => length = value;
        }

        /// <summary>World position of the blade tip (for trails and smears later).</summary>
        public Vector3 TipPosition { get; private set; }

        private void OnEnable()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 2;
        }

        private void LateUpdate()
        {
            if (rig == null || !rig.IsBuilt) return;

            Vector3 hand = rig.HandPosition(front: true);
            Vector3 elbow = rig.Pivot(PoseJoint.ElbowFront).position;
            Vector3 dir = (hand - elbow).normalized;
            float scale = Mathf.Abs(transform.lossyScale.y);
            TipPosition = hand + dir * (length * scale);

            line.SetPosition(0, hand);
            line.SetPosition(1, TipPosition);
            line.widthMultiplier = width * scale;
            if (rig.Proportions != null)
            {
                line.startColor = line.endColor = rig.Proportions.inkColor;
                line.sortingOrder = rig.Proportions.sortingOrder + 2;
            }
            if (line.sharedMaterial == null && rig.LineMaterial != null) line.sharedMaterial = rig.LineMaterial;
        }
    }
}
