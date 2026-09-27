using System.Collections.Generic;
using UnityEngine;
using Pt = Margin.Rendering.WeaponShape.Pt;

namespace Margin.Rendering
{
    /// <summary>
    /// Draws the held weapon from the rig's front hand. The weapon points along the forearm, turned by the pose's
    /// grip angle, and its tip is always exactly hand + direction * Length (what hitboxes, trails and smears use).
    ///
    /// Looks (WeaponLook): Plain (one ink line), Katana (curved gray blade with an ink outline, tapering to a
    /// point, guard, handle, scabbard at the hip, held in both hands) and Pencil (outlined body, sharpened cone,
    /// graphite point, eraser and band).
    ///
    /// Two-hand grip: during play, the back hand is placed on the handle every frame with 2-bone IK, after the
    /// pose is applied and before the rig draws (execution order -40). Off in the editor so posing isn't affected.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class WeaponLine : MonoBehaviour
    {
        [SerializeField] private StickFigureRig rig;
        [SerializeField, Min(0f)] private float length = 0.9f;
        [SerializeField, Min(0.001f)] private float width = 0.035f;
        [Tooltip("How the weapon is drawn. Empty = a plain ink line.")]
        [SerializeField] private WeaponLook look;

        private LineRenderer line;
        private TrailRenderer trail;
        private readonly List<LineRenderer> parts = new List<LineRenderer>();
        private WeaponStyle builtStyle = (WeaponStyle)(-1);
        private bool builtScabbard;
        private AnimationCurve bladeTaper;
        // Which side the katana's back faces (see WeaponShape.EdgeSide), and the blade angle last frame.
        private float edgeSide = 1f, edgeStill, lastAngle, lastFacing, restSide = 1f;
        private bool hasLastAngle;

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

        public WeaponLook Look
        {
            get => look;
            set => look = value;
        }

        /// <summary>
        /// Set by the owner while an attack is running. Only then does the katana's edge follow the swing; the rest of
        /// the time (walking, running, jumping) the curve stays on its natural side, however the arm moves.
        /// </summary>
        public bool Attacking { get; set; }

        /// <summary>World position of the weapon tip.</summary>
        public Vector3 TipPosition { get; private set; }

        /// <summary>The main line (the blade). Used for the tip trail's material and older code.</summary>
        public LineRenderer Line => line;

        /// <summary>Every line this weapon draws (for dash afterimages).</summary>
        public LineRenderer[] Lines
        {
            get
            {
                var all = new LineRenderer[parts.Count + 1];
                all[0] = line;
                for (int i = 0; i < parts.Count; i++) all[i + 1] = parts[i];
                return all;
            }
        }

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
            DestroyParts();
            if (trail == null) return;
            if (Application.isPlaying) Destroy(trail.gameObject);
            else DestroyImmediate(trail.gameObject);
        }

        private void OnEnable()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.numCapVertices = 2;
        }

        /// <summary>
        /// Direction the weapon points: the front forearm's direction turned by the pose's grip angle.
        /// The turn is mirrored with the figure, so a grip looks the same facing either way.
        /// </summary>
        public static Vector3 WeaponDirection(StickFigureRig rig)
        {
            Vector3 hand = rig.HandPosition(front: true);
            Vector3 elbow = rig.Pivot(PoseJoint.ElbowFront).position;
            Vector3 dir = (hand - elbow).normalized;
            float facing = Mathf.Sign(rig.transform.lossyScale.x);
            return Quaternion.Euler(0f, 0f, -rig.GripAngle * facing) * dir;
        }

        private void LateUpdate()
        {
            if (rig == null || !rig.IsBuilt) return;

            WeaponStyle style = look != null ? look.style : WeaponStyle.Plain;
            bool scabbard = look != null && look.scabbard && style == WeaponStyle.Katana;
            if (style != builtStyle || scabbard != builtScabbard) BuildParts(style, scabbard);

            Vector3 hand = rig.HandPosition(front: true);
            Vector3 dir = WeaponDirection(rig);
            float scale = Mathf.Abs(transform.lossyScale.y);
            float facing = Mathf.Sign(rig.transform.lossyScale.x);
            TipPosition = hand + dir * (length * scale);
            if (trail != null) trail.transform.position = TipPosition;

            if (look != null && look.twoHanded && Application.isPlaying)
            {
                // Both hands on the handle while the blade is in front or raised; a sword trailing behind
                // (runs, jumps, dashes) is held in one hand. Blended so the back hand never pops.
                float forward = dir.x * facing;
                float weight = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.55f, -0.15f, forward)),
                                         Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.7f, dir.y)));
                if (weight > 0.001f) PlaceBackHand(hand - dir * (look.handleLength * 0.6f * scale), weight);
            }

            Color ink = rig.Tint ?? (rig.Proportions != null ? rig.Proportions.inkColor : Color.black);
            int order = rig.Proportions != null ? rig.Proportions.sortingOrder + 2 : 12;
            if (line.sharedMaterial == null && rig.LineMaterial != null) line.sharedMaterial = rig.LineMaterial;

            var h = new Pt(hand.x, hand.y);
            var d = new Pt(dir.x, dir.y);
            switch (style)
            {
                case WeaponStyle.Katana: DrawKatana(h, d, scale, facing, ink, order); break;
                case WeaponStyle.Pencil: DrawPencil(h, d, scale, ink, order); break;
                default:
                    Set(line, new[] { h, new Pt(TipPosition.x, TipPosition.y) }, width * scale, ink, order);
                    line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
                    break;
            }
            if (scabbard) DrawScabbard(scale, ink, order - 3);
        }

        // ---------------- katana ----------------

        private void DrawKatana(Pt hand, Pt dir, float scale, float facing, Color ink, int order)
        {
            // The blade's back is "up" when it points forward (mirrored with facing), unless it's swinging: then the
            // cutting edge leads the swing, so the curve flips to the side the blade is moving away from.
            // Resting: the back of the blade on top and the edge facing down, whichever way it points. For a blade
            // pointing left that's the opposite side from one pointing right. Near vertical, keep the last side so it
            // doesn't flicker.
            if (Mathf.Abs(dir.X) > 0.25f) restSide = Mathf.Sign(dir.X);
            float side = restSide;
            float angle = Mathf.Atan2(dir.Y, dir.X) * Mathf.Rad2Deg;
            float dt = Time.deltaTime;
            if (look.edgeFollowsSwing && Application.isPlaying && dt > 0f)
            {
                // Turning around mirrors the blade in one frame: that's not a swing, so don't measure across it.
                if (facing != lastFacing)
                {
                    hasLastAngle = false;
                    edgeSide = restSide;
                }
                float turn = hasLastAngle ? Mathf.DeltaAngle(lastAngle, angle) / dt : 0f;
                // Not attacking: no swing counts, and the edge heads straight back to its natural side.
                if (!Attacking)
                {
                    turn = 0f;
                    edgeStill = look.edgeSettleSeconds;
                }
                edgeSide = WeaponShape.EdgeSide(edgeSide, turn, restSide, ref edgeStill, dt, look.swingTurnSpeed,
                                                look.edgeSettleSeconds, look.edgeFlipSeconds);
                side = edgeSide;
            }
            else if (!look.edgeFollowsSwing) edgeSide = restSide;
            lastAngle = angle;
            lastFacing = facing;
            hasLastAngle = true;
            Pt[] blade = WeaponShape.Blade(hand, dir, length * scale, look.curve * scale, side);
            if (bladeTaper == null || Mathf.Abs(bladeTaper.keys[1].time - look.taperStart) > 1e-4f)
                bladeTaper = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(look.taperStart, 1f), new Keyframe(1f, 0.05f));

            // Ink outline behind a gray fill: a gray blade with a black edge.
            Set(line, blade, (look.bladeWidth + 2f * look.outlineWidth) * scale, ink, order);
            line.widthCurve = bladeTaper;
            Set(parts[0], blade, look.bladeWidth * scale, look.bladeFill, order + 1);
            parts[0].widthCurve = bladeTaper;
            Set(parts[1], WeaponShape.Guard(hand, dir, look.guardLength * scale), look.guardWidth * scale, ink, order + 2);
            Set(parts[2], WeaponShape.Handle(hand, dir, look.handleLength * scale), look.handleWidth * scale, ink, order + 2);
        }

        // ---------------- pencil ----------------

        private void DrawPencil(Pt hand, Pt dir, float scale, Color ink, int order)
        {
            WeaponShape.Pencil p = WeaponShape.PencilShape(hand, dir, length * scale, look.pencilBackLength * scale,
                                                          look.pencilWidth * scale, look.coneLength * scale, look.eraserLength * scale);
            float w = look.pencilLineWidth * scale;
            Set(line, p.TopEdge, w, ink, order);
            Set(parts[0], p.BottomEdge, w, ink, order);
            Set(parts[1], p.Facet, w * 0.6f, ink, order);
            Set(parts[2], p.Cone, w, ink, order);
            Set(parts[3], p.Graphite, w * 2.2f, ink, order + 1);
            parts[3].widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f));
            Set(parts[4], p.Eraser, w, ink, order);
            parts[4].loop = true;
            Set(parts[5], p.Band, w * 1.6f, ink, order);
        }

        // ---------------- scabbard ----------------

        /// <summary>An outlined sheath hanging from the hips, angled back along the body, with a collar at its mouth.</summary>
        private void DrawScabbard(float scale, Color ink, int order)
        {
            Transform hips = rig.HipsTransform;
            Vector3 mouth = hips.position;
            // The spine's "up" direction in world space, then turned toward the back by scabbardAngle.
            Vector3 up = (rig.ChestPosition - hips.position).normalized;
            float facing = Mathf.Sign(rig.transform.lossyScale.x);
            Vector3 along = Quaternion.Euler(0f, 0f, look.scabbardAngle * facing) * up;
            var m = new Pt(mouth.x, mouth.y);
            var a = new Pt(along.x, along.y);
            Pt side = WeaponShape.Perpendicular(a, 1f) * (look.scabbardWidth * 0.5f * scale);
            Pt end = m + a * (look.scabbardLength * scale);
            Pt tipEnd = end + a * (look.scabbardWidth * 0.5f * scale);
            LineRenderer outline = parts[parts.Count - 2], collar = parts[parts.Count - 1];
            Set(outline, new[] { m + side, end + side, tipEnd, end - side, m - side }, look.outlineWidth * 1.4f * scale, ink, order);
            outline.loop = true;
            Pt c = m + a * (0.06f * scale);
            Set(collar, new[] { c + side * 1.25f, c - side * 1.25f }, look.guardWidth * scale, ink, order);
        }

        // ---------------- two-hand grip ----------------

        /// <summary>
        /// Bends the back arm so its hand reaches <paramref name="target"/> (on the handle). Solved in the rig's
        /// own right-facing space, then written as joint angles like a pose (arm angle = spine angle + shoulder).
        /// </summary>
        private void PlaceBackHand(Vector3 target, float weight)
        {
            Transform root = rig.transform;
            Vector3 hips = root.InverseTransformPoint(rig.HipsTransform.position);
            Vector3 chest = root.InverseTransformPoint(rig.ChestPosition);
            Vector3 goal = root.InverseTransformPoint(target);
            float ua = rig.Proportions.upperArm, fa = rig.Proportions.forearm;

            // Angles measured from "straight down", positive toward facing (the pose convention).
            float spineAngle = Mathf.Atan2(-(chest.x - hips.x), chest.y - hips.y) * Mathf.Rad2Deg;
            Vector2 toGoal = goal - chest;
            float dist = Mathf.Clamp(toGoal.magnitude, Mathf.Abs(ua - fa) + 1e-4f, ua + fa - 1e-4f);
            float toGoalAngle = Mathf.Atan2(toGoal.x, -toGoal.y) * Mathf.Rad2Deg;
            // Law of cosines: angle at the shoulder between the goal line and the upper arm.
            float shoulderOffset = Mathf.Acos(Mathf.Clamp((ua * ua + dist * dist - fa * fa) / (2f * ua * dist), -1f, 1f)) * Mathf.Rad2Deg;

            // Elbows bend forward (positive elbow angle): pick the solution that does.
            float upper = toGoalAngle + shoulderOffset;
            Vector2 elbow = (Vector2)chest + new Vector2(Mathf.Sin(upper * Mathf.Deg2Rad), -Mathf.Cos(upper * Mathf.Deg2Rad)) * ua;
            Vector2 fore = (Vector2)goal - elbow;
            float elbowAngle = FigurePose.DeltaAngle(upper, Mathf.Atan2(fore.x, -fore.y) * Mathf.Rad2Deg);
            if (elbowAngle < 0f)
            {
                upper = toGoalAngle - shoulderOffset;
                elbow = (Vector2)chest + new Vector2(Mathf.Sin(upper * Mathf.Deg2Rad), -Mathf.Cos(upper * Mathf.Deg2Rad)) * ua;
                fore = (Vector2)goal - elbow;
                elbowAngle = FigurePose.DeltaAngle(upper, Mathf.Atan2(fore.x, -fore.y) * Mathf.Rad2Deg);
            }

            float shoulder = FigurePose.DeltaAngle(spineAngle, upper);
            // Blend from the pose's own back arm toward the grip.
            Transform s = rig.Pivot(PoseJoint.ShoulderBack), e = rig.Pivot(PoseJoint.ElbowBack);
            float poseShoulder = StickFigureRig.Sign(PoseJoint.ShoulderBack) * s.localEulerAngles.z;
            float poseElbow = StickFigureRig.Sign(PoseJoint.ElbowBack) * e.localEulerAngles.z;
            shoulder = FigurePose.LerpAngle(poseShoulder, shoulder, weight);
            elbowAngle = FigurePose.LerpAngle(poseElbow, elbowAngle, weight);
            s.localRotation = Quaternion.Euler(0f, 0f, StickFigureRig.Sign(PoseJoint.ShoulderBack) * shoulder);
            e.localRotation = Quaternion.Euler(0f, 0f, StickFigureRig.Sign(PoseJoint.ElbowBack) * elbowAngle);
        }

        // ---------------- line helpers ----------------

        private void BuildParts(WeaponStyle style, bool scabbard)
        {
            DestroyParts();
            int count = style == WeaponStyle.Katana ? 3 : style == WeaponStyle.Pencil ? 6 : 0;
            if (scabbard) count += 2;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("WeaponPart" + i) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.numCapVertices = 2;
                lr.numCornerVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.sharedMaterial = line.sharedMaterial != null ? line.sharedMaterial : InkMaterial.Runtime;
                parts.Add(lr);
            }
            builtStyle = style;
            builtScabbard = scabbard;
        }

        private void DestroyParts()
        {
            foreach (LineRenderer lr in parts)
            {
                if (lr == null) continue;
                if (Application.isPlaying) Destroy(lr.gameObject);
                else DestroyImmediate(lr.gameObject);
            }
            parts.Clear();
            builtStyle = (WeaponStyle)(-1);
        }

        private static void Set(LineRenderer lr, Pt[] points, float lineWidth, Color color, int order)
        {
            lr.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++) lr.SetPosition(i, new Vector3(points[i].X, points[i].Y, 0f));
            lr.loop = false;
            lr.widthMultiplier = lineWidth;
            lr.startColor = lr.endColor = color;
            lr.sortingOrder = order;
            if (lr.widthCurve == null || lr.widthCurve.length == 0) lr.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        }
    }
}
