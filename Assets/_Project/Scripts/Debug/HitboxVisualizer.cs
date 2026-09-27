using System.Collections.Generic;
using Margin.Physics;
using Margin.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Margin.DebugTools
{
    /// <summary>
    /// F1 view: draws every 2D collider's real shape over the game so you can compare it with the art.
    /// Colors: red = Ground, blue = OneWayPlatform, green = the player's collision box at its tick position,
    /// yellow = anything else. Combat hitboxes/hurtboxes get added here in Milestone 3.
    ///
    /// Drawn with GL lines after each camera renders. Works with the built-in renderer (Camera.onPostRender)
    /// and with URP (RenderPipelineManager.endCameraRendering).
    /// </summary>
    public sealed class HitboxVisualizer : MonoBehaviour
    {
        private static readonly Color GroundColor = new Color(1f, 0.25f, 0.25f);
        private static readonly Color OneWayColor = new Color(0.25f, 0.5f, 1f);
        private static readonly Color PlayerColor = new Color(0.1f, 0.85f, 0.2f);
        private static readonly Color OtherColor = new Color(1f, 0.8f, 0.1f);

        private readonly List<Collider2D> colliders = new List<Collider2D>();
        private Material lineMaterial;
        private float nextRefresh;
        private int groundLayer, oneWayLayer;

        public bool Visible { get; set; }
        public PlayerController Player { get; set; }

        private void OnEnable()
        {
            groundLayer = LayerMask.NameToLayer("Ground");
            oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
            Camera.onPostRender += OnPostRenderCamera;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            Camera.onPostRender -= OnPostRenderCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        private void OnDestroy()
        {
            if (lineMaterial != null) Destroy(lineMaterial);
        }

        private void OnPostRenderCamera(Camera cam) => Draw(cam);
        private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam) => Draw(cam);

        private void Draw(Camera cam)
        {
            if (!Visible || cam.cameraType != CameraType.Game) return;
            RefreshCollidersIfDue();
            if (!EnsureMaterial()) return;

            lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;
            GL.Begin(GL.LINES);

            KinematicBody2D playerBody = Player != null ? Player.Body : null;
            foreach (Collider2D col in colliders)
            {
                if (col == null || !col.enabled) continue;
                if (playerBody != null && col.gameObject == playerBody.gameObject) continue;   // drawn below
                DrawCollider(col, ColorFor(col.gameObject.layer));
            }

            if (playerBody != null) DrawPlayer(playerBody);

            GL.End();
            GL.PopMatrix();
        }

        private void DrawPlayer(KinematicBody2D body)
        {
            // Drawn at the tick position (what collision uses), not the smoothed render position.
            Vector2 half = body.Size / 2f;
            Vector2 center = body.Position + body.GetComponent<BoxCollider2D>().offset;
            GL.Color(PlayerColor);
            Rect(center - half, center + half);

            if (body.Collisions.Grounded)
            {
                // Short line showing the ground normal (slope direction).
                Vector2 feet = new Vector2(center.x, center.y - half.y);
                Line(feet, feet + body.Collisions.GroundNormal * 0.5f);
            }
        }

        private static void DrawCollider(Collider2D col, Color color)
        {
            GL.Color(color);
            Transform t = col.transform;

            if (col is BoxCollider2D box)
            {
                Vector2 h = box.size / 2f;
                Vector2 o = box.offset;
                Vector3 a = t.TransformPoint(o + new Vector2(-h.x, -h.y));
                Vector3 b = t.TransformPoint(o + new Vector2(-h.x, h.y));
                Vector3 c = t.TransformPoint(o + new Vector2(h.x, h.y));
                Vector3 d = t.TransformPoint(o + new Vector2(h.x, -h.y));
                Line(a, b); Line(b, c); Line(c, d); Line(d, a);
            }
            else if (col is PolygonCollider2D poly)
            {
                for (int p = 0; p < poly.pathCount; p++)
                {
                    Vector2[] points = poly.GetPath(p);
                    for (int i = 0; i < points.Length; i++)
                    {
                        Vector3 from = t.TransformPoint(points[i] + poly.offset);
                        Vector3 to = t.TransformPoint(points[(i + 1) % points.Length] + poly.offset);
                        Line(from, to);
                    }
                }
            }
        }

        private Color ColorFor(int layer)
        {
            if (layer == groundLayer) return GroundColor;
            if (layer == oneWayLayer) return OneWayColor;
            return OtherColor;
        }

        private void RefreshCollidersIfDue()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1f;

            colliders.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    colliders.AddRange(root.GetComponentsInChildren<Collider2D>());
            }
        }

        private bool EnsureMaterial()
        {
            if (lineMaterial != null) return true;
            // Unity's built-in unlit vertex-color shader, available in every project and pipeline.
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) return false;

            lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
            lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            return true;
        }

        private static void Rect(Vector2 min, Vector2 max)
        {
            Line(new Vector2(min.x, min.y), new Vector2(min.x, max.y));
            Line(new Vector2(min.x, max.y), new Vector2(max.x, max.y));
            Line(new Vector2(max.x, max.y), new Vector2(max.x, min.y));
            Line(new Vector2(max.x, min.y), new Vector2(min.x, min.y));
        }

        private static void Line(Vector3 from, Vector3 to)
        {
            GL.Vertex(from);
            GL.Vertex(to);
        }
    }
}
