using System.Collections.Generic;
using Margin.Core;
using Margin.FX;
using UnityEngine;

namespace Margin.Combat
{
    /// <summary>
    /// The Brush Katana special (spec 7): a crescent of ink that flies forward, hitting each enemy it passes
    /// through once. Uses its AttackData for damage, knockback, hitstun and hitstop. Ticks with the GameLoop.
    /// Passes through walls for now (level collision comes with rooms in Milestone 5).
    /// </summary>
    public sealed class InkWaveProjectile : MonoBehaviour, ITickable
    {
        private static readonly Color Ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);

        private Component owner;
        private AttackData attack;
        private CombatSettings settings;
        private Vector2 position;
        private int facing;
        private int lifeLeft;
        private SmearRenderer visual;
        private readonly HashSet<IHitReceiver> hit = new HashSet<IHitReceiver>();

        public int TickOrder => 15;
        public Vector2 Position => position;
        public int HitCount => hit.Count;

        public static InkWaveProjectile Spawn(Component owner, AttackData attack, Vector2 position, int facing, CombatSettings settings)
        {
            var go = new GameObject("InkWave");
            var wave = go.AddComponent<InkWaveProjectile>();
            wave.owner = owner;
            wave.attack = attack;
            wave.settings = settings;
            wave.position = position;
            wave.facing = facing < 0 ? -1 : 1;
            wave.lifeLeft = attack.projectileLifetimeFrames;
            wave.visual = SmearRenderer.Create(sortingOrder: 14);
            wave.Draw();
            return wave;
        }

        private void OnEnable() => GameLoop.Register(this);
        private void OnDisable() => GameLoop.Unregister(this);

        private void OnDestroy()
        {
            if (visual != null) Destroy(visual.gameObject);
        }

        public AabbBox Box => new AabbBox(position.x, position.y, attack.projectileSize.x, attack.projectileSize.y);

        public void Tick()
        {
            position.x += facing * attack.projectileSpeed * GameTime.TickDelta;
            HitResolver.Resolve(owner, Faction.Player, attack, Box, facing, hit, settings);
            Draw();

            if (--lifeLeft <= 0) Destroy(gameObject);
        }

        /// <summary>A crescent facing its direction of travel, redrawn every tick; fades in its last frames.</summary>
        private void Draw()
        {
            float radius = attack.projectileSize.y * 0.55f;
            Vector3 pivot = new Vector3(position.x - facing * radius * 0.6f, position.y, 0f);
            float from = facing > 0 ? -70f : 110f;
            float to = facing > 0 ? 70f : 250f;
            Color c = Ink;
            c.a = Mathf.Clamp01(lifeLeft / 8f) * 0.9f;
            visual.Show(pivot, from, to, radius, 0.55f, c, 2);
        }
    }
}
