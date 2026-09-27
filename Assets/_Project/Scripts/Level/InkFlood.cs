using System.Collections.Generic;
using Margin.Combat;
using Margin.Core;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Level
{
    /// <summary>
    /// A pool of glowing highlighter ink that can rise over a floor (spec 10: the Highlighter's phase 2 floods the
    /// lower arena, so the player must stay on the raised lines). Touching it hurts and bounces the player up;
    /// its AttackData should use a 1 frame hitstun so the hurt invulnerability starts at once and the player can
    /// steer onto a platform. Someone else (the boss) sets how full it is with Level01.
    /// </summary>
    public sealed class InkFlood : MonoBehaviour, ITickable, IHitboxSource
    {
        [SerializeField] private AttackData attack;
        [SerializeField] private CombatSettings settings;
        [Tooltip("Width of the pool, centered on this object.")]
        [SerializeField, Min(0.1f)] private float width = 36f;
        [Tooltip("Bottom of the pool, relative to this object (below the floor, out of sight).")]
        [SerializeField] private float bottom = -2.5f;
        [Tooltip("Surface height when completely full, relative to this object.")]
        [SerializeField] private float fullLevel = 1.4f;
        [Tooltip("Surface height when empty (under the floor).")]
        [SerializeField] private float emptyLevel = -1f;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color glow = new Color(1f, 0.93f, 0.35f, 0.82f);
        [SerializeField] private Color surfaceColor = new Color32(0xE0, 0xB8, 0x1E, 0xFF);

        private readonly HashSet<IHitReceiver> hit = new HashSet<IHitReceiver>();
        private readonly List<IHitReceiver> release = new List<IHitReceiver>();
        private readonly List<AabbBox> boxes = new List<AabbBox>();
        private LineRenderer fill, surface;
        private float level01;
        private int tick;

        public int TickOrder => 25;

        /// <summary>0 = empty (harmless, hidden), 1 = full.</summary>
        public float Level01
        {
            get => level01;
            set
            {
                level01 = Mathf.Clamp01(value);
                Draw();
            }
        }

        public bool Active => level01 > 0.001f;
        public float SurfaceY => transform.position.y + Mathf.Lerp(emptyLevel, fullLevel, level01);
        public float FullSurfaceY => transform.position.y + fullLevel;

        public void Configure(AttackData hitAttack, CombatSettings combatSettings, float poolWidth, float full)
        {
            attack = hitAttack;
            settings = combatSettings;
            width = poolWidth;
            fullLevel = full;
        }

        private void Awake() => Build();

        private void OnEnable()
        {
            GameLoop.Register(this);
            HitboxSources.Register(this);
        }

        private void OnDisable()
        {
            GameLoop.Unregister(this);
            HitboxSources.Unregister(this);
            hit.Clear();
        }

        private AabbBox Box
        {
            get
            {
                float x = transform.position.x, b = transform.position.y + bottom, top = SurfaceY;
                return new AabbBox(x, (b + top) * 0.5f, width, Mathf.Max(0.01f, top - b));
            }
        }

        public void Tick()
        {
            tick++;
            boxes.Clear();
            if (!Active || attack == null)
            {
                hit.Clear();
                return;
            }

            AabbBox box = Box;
            boxes.Add(box);
            // Anyone who left the ink, or is invulnerable, can be hit again next time they're in it.
            release.Clear();
            foreach (Hurtbox h in Hurtbox.Active)
            {
                IHitReceiver r = h != null ? h.Receiver : null;
                if (r == null || !hit.Contains(r)) continue;
                if (!r.CanBeHit || !HitboxMath.Overlaps(box, h.WorldBox)) release.Add(r);
            }
            foreach (IHitReceiver r in release) hit.Remove(r);

            HitResolver.Resolve(this, Faction.Enemy, attack, box, 1, hit, settings != null ? settings : CombatSettings.Defaults);
            if (tick % 3 == 0) Draw();
        }

        public Faction Faction => Faction.Enemy;
        public IReadOnlyList<AabbBox> ActiveHitboxes => boxes;

        private void Build()
        {
            if (fill != null) return;
            fill = Line("Glow", glow, 15);
            surface = Line("Surface", surfaceColor, 16);
            surface.widthMultiplier = 0.09f;
            Draw();
        }

        private LineRenderer Line(string lineName, Color color, int order)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = lineMaterial != null ? lineMaterial : InkMaterial.Runtime;
            line.startColor = line.endColor = color;
            line.numCapVertices = 0;
            line.sortingOrder = order;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        /// <summary>A flat band of glow from the bottom to the surface, with a gently waving ink line on top.</summary>
        private void Draw()
        {
            if (fill == null) return;
            bool show = Active;
            fill.enabled = surface.enabled = show;
            if (!show) return;

            float top = Mathf.Lerp(emptyLevel, fullLevel, level01);
            float depth = top - bottom;
            fill.widthMultiplier = depth;
            fill.positionCount = 2;
            fill.SetPosition(0, new Vector3(-width * 0.5f, bottom + depth * 0.5f, 0f));
            fill.SetPosition(1, new Vector3(width * 0.5f, bottom + depth * 0.5f, 0f));

            int points = Mathf.Max(2, Mathf.CeilToInt(width / 0.5f) + 1);
            surface.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float x = -width * 0.5f + width * i / (points - 1);
                float wave = 0.07f * Mathf.Sin(x * 1.3f + tick * 0.09f) + 0.04f * Mathf.Sin(x * 3.1f - tick * 0.13f);
                surface.SetPosition(i, new Vector3(x, top + wave, 0f));
            }
        }
    }
}
