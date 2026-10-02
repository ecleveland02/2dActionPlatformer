using Margin.Player;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Abilities
{
    /// <summary>
    /// Draws the Spring Doodle (spec 8): when you double jump, a little ink spring is scribbled at your feet where
    /// you jumped from, boings out and fades. Added to the player automatically; purely visual.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(PlayerController))]
    public sealed class SpringFX : MonoBehaviour
    {
        private const int Frames = 14, Coils = 5;
        private static readonly Color Ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);

        private PlayerController player;
        private LineRenderer coil;
        private Vector2 origin;
        private int age = Frames, lastSpring;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            var go = new GameObject("Spring Doodle");
            go.transform.SetParent(transform, false);
            coil = go.AddComponent<LineRenderer>();
            coil.useWorldSpace = true;
            coil.sharedMaterial = InkMaterial.Runtime;
            coil.widthMultiplier = 0.05f;
            coil.numCornerVertices = 2;
            coil.sortingOrder = 9;
            coil.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            coil.receiveShadows = false;
            coil.enabled = false;
        }

        private void FixedUpdate()
        {
            if (player.Combat == null || player.Body == null) return;
            int spring = player.Combat.SpringFramesLeft;
            if (spring > lastSpring)
            {
                // A new double jump: draw the spring where the feet were.
                origin = player.Body.Position - new Vector2(0f, player.Body.Size.y * 0.5f);
                age = 0;
            }
            lastSpring = spring;
            if (!GameLoopRunning()) return;
            if (age < Frames) age++;
        }

        private static bool GameLoopRunning() => !Margin.Core.GameLoop.Paused;

        private void LateUpdate()
        {
            coil.enabled = age < Frames;
            if (!coil.enabled) return;
            float t = age / (float)Frames;
            float height = Mathf.Lerp(0.15f, 0.7f, Mathf.Sin(Mathf.Min(1f, t * 1.6f) * Mathf.PI * 0.5f));
            const int perCoil = 2;
            int count = Coils * perCoil + 1;
            coil.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float u = i / (float)(count - 1);
                float x = (i % 2 == 0 ? -1f : 1f) * 0.18f;
                if (i == 0 || i == count - 1) x = 0f;
                coil.SetPosition(i, new Vector3(origin.x + x, origin.y - height + u * height, 0f));
            }
            Color c = Ink;
            c.a = 1f - t * t;
            coil.startColor = coil.endColor = c;
        }
    }
}
