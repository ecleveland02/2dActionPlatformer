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
        private TrailRenderer trail;

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

        /// <summary>World position of the blade tip.</summary>
        public Vector3 TipPosition { get; private set; }

        public LineRenderer Line => line;

        /// <summary>Whether the tip trail is drawing (on during attacks).</summary>
        public bool TrailEmitting
        {
            get => trail != null && trail.emitting;
            set
            {
                if (trail == null) return;
                if (trail.emitting && !value) trail.emitting = false;
                else if (!trail.emitting && value)
                {
                    trail.Clear();   // don't connect to wherever the tip was when the last trail ended
                    trail.emitting = true;
                }
            }
        }

        /// <summary>
        /// Creates the tip trail (spec 4.3: ink-colored, short lifetime). Safe to call again to update settings.
        /// </summary>
        public void ConfigureTrail(float time, float width, Color ink, int sortingOrder)
        {
            if (trail == null)
            {
                var tip = new GameObject("BladeTrail");
                tip.hideFlags = HideFlags.DontSave;
                trail = tip.AddComponent<TrailRenderer>();
                trail.emitting = false;
                trail.minVertexDistance = 0.02f;
                trail.numCapVertices = 2;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
            }
            trail.sharedMaterial = line != null && line.sharedMaterial != null ? line.sharedMaterial : InkMaterial.Runtime;
            trail.time = time;
            trail.widthMultiplier = width;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(ink, 0f), new GradientColorKey(ink, 1f) },
                             new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.sortingOrder = sortingOrder;
        }

        private void OnDestroy()
        {
            if (trail == null) return;
            if (Application.isPlaying) Destroy(trail.gameObject);
            else DestroyImmediate(trail.gameObject);
        }

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
            if (trail != null) trail.transform.position = TipPosition;
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
