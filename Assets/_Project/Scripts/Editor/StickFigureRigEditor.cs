using Margin.Rendering;
using UnityEditor;
using UnityEngine;

namespace Margin.EditorTools
{
    /// <summary>
    /// The pose editor (spec 4.2). Select a StickFigureRig, then in the Scene view:
    ///   - click a joint dot to select it (front limbs dark, back limbs grey, selected orange),
    ///   - drag the ring around the selected joint to rotate it,
    ///   - drag the square on the hips to shift the root (crouch/bob).
    /// Inspector: Load / Save / Save As New / Mirror / Reset, plus exact angle entry.
    /// Every change supports Undo (Ctrl+Z).
    /// </summary>
    [CustomEditor(typeof(StickFigureRig))]
    public sealed class StickFigureRigEditor : UnityEditor.Editor
    {
        private const string PoseFolder = "Assets/_Project/Data/Poses";

        private static readonly Color FrontColor = new Color(0.1f, 0.1f, 0.1f);
        private static readonly Color BackColor = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color SelectedColor = new Color(1f, 0.55f, 0.1f);

        // Static so the selection survives reselecting the object.
        private static PoseJoint selected = PoseJoint.ShoulderFront;

        private StickFigureRig Rig => (StickFigureRig)target;

        // ---------------- Scene view ----------------

        private void OnSceneGUI()
        {
            StickFigureRig rig = Rig;
            if (!rig.IsBuilt) return;

            foreach (PoseJoint joint in FigurePose.AllJoints)
            {
                Transform pivot = rig.Pivot(joint);
                float size = HandleUtility.GetHandleSize(pivot.position) * 0.06f;
                Handles.color = joint == selected ? SelectedColor : IsBack(joint) ? BackColor : FrontColor;
                if (Handles.Button(pivot.position, Quaternion.identity, size, size * 1.4f, Handles.DotHandleCap))
                {
                    selected = joint;
                    Repaint();
                }
            }

            DrawRotationRing(rig);
            DrawHipsHandle(rig);
        }

        private void DrawRotationRing(StickFigureRig rig)
        {
            Transform pivot = rig.Pivot(selected);
            float radius = HandleUtility.GetHandleSize(pivot.position) * 0.6f;

            Handles.color = SelectedColor;
            EditorGUI.BeginChangeCheck();
            // Snap 1°: hold Ctrl while dragging for Unity's rotation snapping increments.
            Quaternion rotated = Handles.Disc(pivot.rotation, pivot.position, Vector3.forward, radius, false, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(pivot, "Rotate " + selected);
                pivot.rotation = rotated;
            }

            float angle = rig.CapturePose().Get(selected);
            Handles.Label(pivot.position + Vector3.up * radius * 1.1f, $"{selected}  {angle:0}°");
        }

        private void DrawHipsHandle(StickFigureRig rig)
        {
            Transform hips = rig.HipsTransform;
            float size = HandleUtility.GetHandleSize(hips.position) * 0.08f;

            Handles.color = new Color(0.2f, 0.6f, 1f);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider2D(hips.position, Vector3.forward, Vector3.right, Vector3.up, size,
                                             Handles.RectangleHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(rig, "Move hips");
                Vector3 local = rig.transform.InverseTransformVector(moved - hips.position);
                rig.RootOffset += new Vector2(local.x, local.y);
                EditorUtility.SetDirty(rig);
            }
        }

        private static bool IsBack(PoseJoint joint) =>
            joint == PoseJoint.ShoulderBack || joint == PoseJoint.ElbowBack ||
            joint == PoseJoint.HipBack || joint == PoseJoint.KneeBack;

        // ---------------- Inspector ----------------

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            StickFigureRig rig = Rig;
            if (!rig.IsBuilt)
            {
                if (GUILayout.Button("Build Rig")) rig.Build();
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Pose Editor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Scene view: click a joint dot, drag its orange ring. Drag the blue square to move the hips.\n" +
                                    "Positive angles swing forward. Knees bend negative, elbows positive.", MessageType.None);

            // Exact angle entry for the selected joint.
            selected = (PoseJoint)EditorGUILayout.EnumPopup("Selected joint", selected);
            FigurePose current = rig.CapturePose();
            EditorGUI.BeginChangeCheck();
            float typed = EditorGUILayout.FloatField("Angle (°)", current.Get(selected));
            Vector2 offset = EditorGUILayout.Vector2Field("Hips offset", rig.RootOffset);
            if (EditorGUI.EndChangeCheck())
            {
                current.Set(selected, typed);
                current.rootOffsetX = offset.x;
                current.rootOffsetY = offset.y;
                ApplyWithUndo(rig, current, "Edit pose");
            }

            EditorGUILayout.Space();
            PoseData poseAsset = rig.EditingPose;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = poseAsset != null;
                if (GUILayout.Button("Load")) ApplyWithUndo(rig, poseAsset.pose, "Load pose " + poseAsset.name);
                if (GUILayout.Button("Save")) SavePose(poseAsset, rig.CapturePose());
                GUI.enabled = true;
                if (GUILayout.Button("Save As New...")) SaveAsNew(rig);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Mirror")) ApplyWithUndo(rig, rig.CapturePose().Mirrored(), "Mirror pose");
                if (GUILayout.Button("Reset to Neutral")) ApplyWithUndo(rig, FigurePose.Neutral, "Reset pose");
                if (GUILayout.Button("Rebuild Rig")) rig.Build();
            }
        }

        private static void ApplyWithUndo(StickFigureRig rig, FigurePose pose, string label)
        {
            Undo.RecordObject(rig, label);
            foreach (PoseJoint joint in FigurePose.AllJoints) Undo.RecordObject(rig.Pivot(joint), label);
            rig.ApplyPose(pose);
            EditorUtility.SetDirty(rig);
            SceneView.RepaintAll();
        }

        private static void SavePose(PoseData asset, FigurePose pose)
        {
            Undo.RecordObject(asset, "Save pose");
            asset.pose = pose;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"Saved pose '{asset.name}'.", asset);
        }

        private static void SaveAsNew(StickFigureRig rig)
        {
            EnsureFolder(PoseFolder);
            string path = EditorUtility.SaveFilePanelInProject("Save Pose As", "NewPose", "asset",
                                                               "Name the new pose.", PoseFolder);
            if (string.IsNullOrEmpty(path)) return;

            var asset = AssetDatabase.LoadAssetAtPath<PoseData>(path);
            if (asset == null)
            {
                asset = CreateInstance<PoseData>();
                asset.pose = rig.CapturePose();
                AssetDatabase.CreateAsset(asset, path);
            }
            else
            {
                asset.pose = rig.CapturePose();
                EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();

            Undo.RecordObject(rig, "Set editing pose");
            rig.EditingPose = asset;
            EditorUtility.SetDirty(rig);
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"Saved new pose at {path}.", asset);
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
