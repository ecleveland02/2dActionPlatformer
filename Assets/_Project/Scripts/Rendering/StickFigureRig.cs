using UnityEngine;

namespace Margin.Rendering
{
    /// <summary>
    /// Procedural stick figure (spec 4.1). Builds a joint hierarchy under itself and draws it with
    /// LineRenderers every frame, so it can be posed in the Scene view (the component runs in edit mode too).
    ///
    /// Hierarchy (right-facing, origin = body center, feet at -feetBelowOrigin):
    ///   Hips (moved by the pose's root offset)
    ///     Spine pivot -> Chest
    ///       Neck pivot -> head
    ///       ShoulderFront -> ElbowFront -> HandFront      (same for Back)
    ///     HipFront -> KneeFront -> FootFront                (same for Back)
    /// Each pivot's local Z rotation holds one PoseJoint angle. Positive angles swing forward: bones that
    /// point down (arms, legs) use +Z, bones that point up (spine, neck) use -Z.
    /// </summary>
    [ExecuteAlways]
    public sealed class StickFigureRig : MonoBehaviour
    {
        [SerializeField] private StickFigureProportions proportions;
        [Tooltip("Optional persistent material for the lines. If empty, one is created at runtime.")]
        [SerializeField] private Material lineMaterial;
        [Tooltip("Pose shown/edited in the pose editor (Inspector buttons Load / Save).")]
        [SerializeField] private PoseData editingPose;

        // Generated joints. Serialized so the hierarchy survives saving the scene.
        [SerializeField, HideInInspector] private Transform hips, spinePivot, chest, neckPivot, headCenter;
        [SerializeField, HideInInspector] private Transform shoulderFront, elbowFront, handFront;
        [SerializeField, HideInInspector] private Transform shoulderBack, elbowBack, handBack;
        [SerializeField, HideInInspector] private Transform hipFront, kneeFront, footFront;
        [SerializeField, HideInInspector] private Transform hipBack, kneeBack, footBack;
        [Tooltip("Hips shift from the neutral position (crouch, bob). Set by poses and the pose editor's hips handle.")]
        [SerializeField, HideInInspector] private Vector2 rootOffset;
        [SerializeField, HideInInspector] private LineRenderer spineLine, headLine, armFrontLine, armBackLine, legFrontLine, legBackLine;

        private static StickFigureProportions defaultProportions;
        private Vector3[] headPoints;

        public StickFigureProportions Proportions
        {
            get
            {
                if (proportions != null) return proportions;
                if (defaultProportions == null)
                {
                    defaultProportions = ScriptableObject.CreateInstance<StickFigureProportions>();
                    defaultProportions.hideFlags = HideFlags.DontSave;
                }
                return defaultProportions;
            }
            set => proportions = value;
        }

        public Material LineMaterial
        {
            get => lineMaterial;
            set => lineMaterial = value;
        }

        public PoseData EditingPose
        {
            get => editingPose;
            set => editingPose = value;
        }

        /// <summary>Overrides the ink colors (e.g. red flash on an unparryable attack's wind-up). Null = normal.</summary>
        public Color? Tint { get; set; }

        public bool IsBuilt => hips != null && footBack != null && legBackLine != null;

        /// <summary>The six ink lines (body, head, arms, legs), e.g. for afterimages to copy.</summary>
        public LineRenderer[] Lines => new[] { legBackLine, armBackLine, spineLine, headLine, legFrontLine, armFrontLine };

        /// <summary>World position of the front shoulder (the sword arm's pivot).</summary>
        public Vector3 ShoulderPosition => shoulderFront.position;

        public Vector3 HipsPosition => hips.position;
        public Vector3 ChestPosition => chest.position;
        public Vector3 HeadCenterPosition => headCenter.position;
        public Vector3 HandPosition(bool front) => (front ? handFront : handBack).position;
        public Vector3 FootPosition(bool front) => (front ? footFront : footBack).position;

        private void OnEnable()
        {
            if (!IsBuilt) Build();
            AssignLineMaterial();
            UpdateBoneLengths();
            DrawLines();
        }

        private void LateUpdate()
        {
            if (!IsBuilt) return;
            UpdateBoneLengths();   // cheap, and makes proportion edits show up immediately
            DrawLines();
        }

        // ---------------- posing ----------------

        /// <summary>The Transform whose local Z rotation stores this joint's angle.</summary>
        public Transform Pivot(PoseJoint joint)
        {
            switch (joint)
            {
                case PoseJoint.Spine: return spinePivot;
                case PoseJoint.Neck: return neckPivot;
                case PoseJoint.ShoulderFront: return shoulderFront;
                case PoseJoint.ElbowFront: return elbowFront;
                case PoseJoint.ShoulderBack: return shoulderBack;
                case PoseJoint.ElbowBack: return elbowBack;
                case PoseJoint.HipFront: return hipFront;
                case PoseJoint.KneeFront: return kneeFront;
                case PoseJoint.HipBack: return hipBack;
                case PoseJoint.KneeBack: return kneeBack;
                default: return null;
            }
        }

        public Transform HipsTransform => hips;

        /// <summary>Hips offset from neutral, in the rig's local units.</summary>
        public Vector2 RootOffset
        {
            get => rootOffset;
            set
            {
                rootOffset = value;
                if (IsBuilt) hips.localPosition = NeutralHipsPosition() + (Vector3)rootOffset;
            }
        }

        /// <summary>+1 for bones that point down (arms, legs), -1 for bones that point up (spine, neck).</summary>
        public static float Sign(PoseJoint joint) => joint == PoseJoint.Spine || joint == PoseJoint.Neck ? -1f : 1f;

        public void ApplyPose(FigurePose pose)
        {
            if (!IsBuilt) Build();
            RootOffset = new Vector2(pose.rootOffsetX, pose.rootOffsetY);
            // Whole-body tilt: the hips are the root of every joint. Positive tips toward facing (clockwise, -z).
            hips.localRotation = Quaternion.Euler(0f, 0f, -pose.rootRotation);
            foreach (PoseJoint joint in FigurePose.AllJoints)
                Pivot(joint).localRotation = Quaternion.Euler(0f, 0f, Sign(joint) * pose.Get(joint));
        }

        /// <summary>Reads the current joint rotations back into a FigurePose (used by the pose editor's Save).</summary>
        public FigurePose CapturePose()
        {
            if (!IsBuilt) Build();
            var pose = new FigurePose
            {
                rootOffsetX = rootOffset.x,
                rootOffsetY = rootOffset.y,
                rootRotation = FigurePose.NormalizeAngle(-hips.localEulerAngles.z),
            };
            foreach (PoseJoint joint in FigurePose.AllJoints)
                pose.Set(joint, FigurePose.NormalizeAngle(Sign(joint) * Pivot(joint).localEulerAngles.z));
            return pose;
        }

        private Vector3 NeutralHipsPosition()
        {
            StickFigureProportions p = Proportions;
            return new Vector3(0f, -p.feetBelowOrigin + p.thigh + p.shin, 0f);
        }

        // ---------------- building ----------------

        /// <summary>Creates (or recreates) the joint hierarchy and line renderers. Keeps the current pose.</summary>
        public void Build()
        {
            FigurePose keep = IsBuilt ? CapturePose() : FigurePose.Neutral;

            Transform old = transform.Find("Joints");
            if (old != null) DestroySafely(old.gameObject);
            old = transform.Find("Lines");
            if (old != null) DestroySafely(old.gameObject);

            Transform joints = NewChild("Joints", transform);
            hips = NewChild("Hips", joints);
            spinePivot = NewChild("Spine", hips);
            chest = NewChild("Chest", spinePivot);
            neckPivot = NewChild("Neck", chest);
            headCenter = NewChild("HeadCenter", neckPivot);
            shoulderFront = NewChild("ShoulderFront", chest);
            elbowFront = NewChild("ElbowFront", shoulderFront);
            handFront = NewChild("HandFront", elbowFront);
            shoulderBack = NewChild("ShoulderBack", chest);
            elbowBack = NewChild("ElbowBack", shoulderBack);
            handBack = NewChild("HandBack", elbowBack);
            hipFront = NewChild("HipFront", hips);
            kneeFront = NewChild("KneeFront", hipFront);
            footFront = NewChild("FootFront", kneeFront);
            hipBack = NewChild("HipBack", hips);
            kneeBack = NewChild("KneeBack", hipBack);
            footBack = NewChild("FootBack", kneeBack);

            Transform lines = NewChild("Lines", transform);
            spineLine = NewLine("Body", lines);
            headLine = NewLine("Head", lines);
            armFrontLine = NewLine("ArmFront", lines);
            armBackLine = NewLine("ArmBack", lines);
            legFrontLine = NewLine("LegFront", lines);
            legBackLine = NewLine("LegBack", lines);
            headLine.loop = true;

            AssignLineMaterial();
            UpdateBoneLengths();
            ApplyPose(keep);
            DrawLines();
        }

        private void UpdateBoneLengths()
        {
            StickFigureProportions p = Proportions;
            hips.localPosition = NeutralHipsPosition() + (Vector3)rootOffset;

            chest.localPosition = new Vector3(0f, p.spine, 0f);
            headCenter.localPosition = new Vector3(0f, p.neck + p.headRadius, 0f);
            elbowFront.localPosition = elbowBack.localPosition = new Vector3(0f, -p.upperArm, 0f);
            handFront.localPosition = handBack.localPosition = new Vector3(0f, -p.forearm, 0f);
            kneeFront.localPosition = kneeBack.localPosition = new Vector3(0f, -p.thigh, 0f);
            footFront.localPosition = footBack.localPosition = new Vector3(0f, -p.shin, 0f);
        }

        // ---------------- drawing ----------------

        private void DrawLines()
        {
            StickFigureProportions p = Proportions;
            float scale = Mathf.Abs(transform.lossyScale.y);
            Color ink = Tint ?? p.inkColor;
            Color back = Tint.HasValue ? Color.Lerp(Tint.Value, Color.white, 0.4f) : p.backLimbColor;

            // The neck line ends where the head circle begins.
            Vector3 neckTop = neckPivot.position + (headCenter.position - neckPivot.position).normalized * (p.neck * scale);
            SetLine(spineLine, ink, p.sortingOrder, hips.position, chest.position, neckTop);
            SetLine(armFrontLine, ink, p.sortingOrder + 1, shoulderFront.position, elbowFront.position, handFront.position);
            SetLine(legFrontLine, ink, p.sortingOrder + 1, hipFront.position, kneeFront.position, footFront.position);
            SetLine(armBackLine, back, p.sortingOrder - 1, shoulderBack.position, elbowBack.position, handBack.position);
            SetLine(legBackLine, back, p.sortingOrder - 1, hipBack.position, kneeBack.position, footBack.position);

            int segments = Mathf.Max(3, p.headSegments);
            if (headPoints == null || headPoints.Length != segments) headPoints = new Vector3[segments];
            Vector3 c = headCenter.position;
            float r = p.headRadius * scale;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                headPoints[i] = c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            headLine.positionCount = segments;
            headLine.SetPositions(headPoints);
            StyleLine(headLine, ink, p.sortingOrder, p.lineWidth * scale, null);
        }

        private void SetLine(LineRenderer line, Color color, int order, Vector3 a, Vector3 b, Vector3 c)
        {
            StickFigureProportions p = Proportions;
            line.positionCount = 3;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            line.SetPosition(2, c);
            StyleLine(line, color, order, p.lineWidth * Mathf.Abs(transform.lossyScale.y), p.widthVariation);
        }

        private static void StyleLine(LineRenderer line, Color color, int order, float width, AnimationCurve curve)
        {
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            line.widthMultiplier = width;
            if (curve != null) line.widthCurve = curve;
        }

        private void AssignLineMaterial()
        {
            if (!IsBuilt) return;
            Material material = lineMaterial != null ? lineMaterial : InkMaterial.Runtime;
            foreach (LineRenderer line in new[] { spineLine, headLine, armFrontLine, armBackLine, legFrontLine, legBackLine })
                line.sharedMaterial = material;
        }


        private static Transform NewChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static LineRenderer NewLine(string name, Transform parent)
        {
            var line = NewChild(name, parent).gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.numCapVertices = 4;       // round caps
            line.numCornerVertices = 3;    // round elbows/knees
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static void DestroySafely(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
