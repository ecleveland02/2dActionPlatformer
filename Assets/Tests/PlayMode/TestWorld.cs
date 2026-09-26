using System.Collections.Generic;
using Margin.Physics;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>
    /// Builds throwaway level geometry for PlayMode tests and destroys it afterwards.
    /// Positions are in units; ground boxes are given by their edges for readability.
    /// </summary>
    public sealed class TestWorld
    {
        public readonly int GroundLayer = LayerMask.NameToLayer("Ground");
        public readonly int OneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
        public readonly int PlayerLayer = LayerMask.NameToLayer("Player");
        public readonly KinematicBodyData BodyData;

        public static readonly Vector2 BodySize = new Vector2(0.6f, 1.8f);
        public const float HalfHeight = 0.9f;

        private readonly List<Object> created = new List<Object>();

        public TestWorld()
        {
            Assert(GroundLayer >= 0 && OneWayLayer >= 0 && PlayerLayer >= 0,
                   "Layers Ground, OneWayPlatform and Player must exist in Tags and Layers.");
            BodyData = ScriptableObject.CreateInstance<KinematicBodyData>();
            BodyData.solidMask = 1 << GroundLayer;
            BodyData.oneWayMask = 1 << OneWayLayer;
            created.Add(BodyData);
        }

        public GameObject Box(float xMin, float yMin, float xMax, float yMax, bool oneWay = false)
        {
            var go = new GameObject("TestBox") { layer = oneWay ? OneWayLayer : GroundLayer };
            go.transform.position = new Vector3((xMin + xMax) / 2f, (yMin + yMax) / 2f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(xMax - xMin, yMax - yMin);
            created.Add(go);
            UnityEngine.Physics2D.SyncTransforms();
            return go;
        }

        public GameObject Polygon(params Vector2[] points)
        {
            var go = new GameObject("TestPolygon") { layer = GroundLayer };
            go.AddComponent<PolygonCollider2D>().points = points;
            created.Add(go);
            UnityEngine.Physics2D.SyncTransforms();
            return go;
        }

        /// <summary>Creates a 0.6 x 1.8 body whose feet are at <paramref name="feet"/>.</summary>
        public KinematicBody2D Body(Vector2 feet)
        {
            var go = new GameObject("TestBody") { layer = PlayerLayer };
            go.transform.position = feet + new Vector2(0f, HalfHeight);
            var body = go.AddComponent<KinematicBody2D>();
            go.GetComponent<BoxCollider2D>().size = BodySize;
            body.Data = BodyData;
            body.Teleport(feet + new Vector2(0f, HalfHeight));
            created.Add(go);
            UnityEngine.Physics2D.SyncTransforms();
            return body;
        }

        public static float Feet(KinematicBody2D body) => body.Position.y - HalfHeight;

        public void Destroy()
        {
            // DestroyImmediate so the next test in the same frame never sees this geometry.
            foreach (Object o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException(message);
        }
    }
}
