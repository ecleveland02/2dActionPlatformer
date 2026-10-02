using System.Collections.Generic;
using Margin.Core;
using Margin.Level;
using Margin.Player;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Combat
{
    /// <summary>
    /// A wave that runs along the floor from a heavy landing (the Stapler Titan's Hop Slam): an ink zigzag
    /// <see cref="height"/> tall that hits anything it passes through once, and stops at walls and platform edges. Jump over it (or
    /// stand on a platform taller than it). Cleared on room change and respawn.
    /// </summary>
    public sealed class ShockwaveProjectile : MonoBehaviour, ITickable, IHitboxSource
    {
        private static readonly Color Ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);

        private Component owner;
        private AttackData attack;
        private CombatSettings settings;
        private float x, floorY, speed, height;
        private int dir, lifeLeft, age;
        private LayerMask solid, floor;
        private readonly HashSet<IHitReceiver> hit = new HashSet<IHitReceiver>();
        private readonly List<AabbBox> boxes = new List<AabbBox>();
        private LineRenderer line;

        public int TickOrder => 24;
        public Faction Faction => Faction.Enemy;
        public IReadOnlyList<AabbBox> ActiveHitboxes => boxes;

        public static ShockwaveProjectile Spawn(Component owner, AttackData attack, CombatSettings settings, float x,
                                                float floorY, int dir, float speed, float height, int lifetimeFrames,
                                                LayerMask solid, LayerMask floor)
        {
            var go = new GameObject("Shockwave");
            var w = go.AddComponent<ShockwaveProjectile>();
            w.owner = owner;
            w.attack = attack;
            w.settings = settings;
            w.x = x;
            w.floorY = floorY;
            w.dir = dir < 0 ? -1 : 1;
            w.speed = speed;
            w.height = height;
            w.lifeLeft = Mathf.Max(1, lifetimeFrames);
            w.solid = solid;
            w.floor = floor;
            w.line = go.AddComponent<LineRenderer>();
            w.line.useWorldSpace = true;
            w.line.sharedMaterial = InkMaterial.Runtime;
            w.line.widthMultiplier = 0.07f;
            w.line.numCornerVertices = 1;
            w.line.sortingOrder = 12;
            w.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            w.line.receiveShadows = false;
            w.Draw();
            return w;
        }

        private void OnEnable()
        {
            GameLoop.Register(this);
            HitboxSources.Register(this);
            LevelEvents.RoomEntered += OnRoomEntered;
            PlayerEvents.Respawned += OnRespawned;
        }

        private void OnDisable()
        {
            GameLoop.Unregister(this);
            HitboxSources.Unregister(this);
            LevelEvents.RoomEntered -= OnRoomEntered;
            PlayerEvents.Respawned -= OnRespawned;
        }

        private void OnRoomEntered(Room room) => Destroy(gameObject);
        private void OnRespawned(PlayerController player) => Destroy(gameObject);

        public void Tick()
        {
            age++;
            float step = speed * GameTime.TickDelta;
            bool wall = UnityEngine.Physics2D.Raycast(new Vector2(x, floorY + 0.3f), new Vector2(dir, 0f), step + 0.3f, solid).collider != null;
            if (!wall) x += dir * step;
            // It runs along whatever it was born on, and dies off the edge of a platform.
            bool ground = UnityEngine.Physics2D.Raycast(new Vector2(x, floorY + 0.15f), Vector2.down, 0.4f, floor).collider != null;

            boxes.Clear();
            var box = new AabbBox(x, floorY + height * 0.5f, 0.7f, height);
            boxes.Add(box);
            HitResolver.Resolve(owner != null ? owner : this, Faction.Enemy, attack, box, dir, hit,
                                settings != null ? settings : CombatSettings.Defaults);
            Draw();
            if (wall || !ground || --lifeLeft <= 0)
            {
                if (FX.InkSplatter.Instance != null) FX.InkSplatter.Instance.Burst(new Vector2(x, floorY + 0.2f), Vector2.up, 4);
                Destroy(gameObject);
            }
        }

        /// <summary>A jagged ink crest leaning the way it travels, flickering a little each tick.</summary>
        private void Draw()
        {
            const int points = 7;
            line.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                float h = height * Mathf.Sin(t * Mathf.PI) * (i % 2 == 0 ? 1f : 0.7f + 0.15f * ((age + i) % 3));
                line.SetPosition(i, new Vector3(x - dir * 0.6f + dir * t * 0.9f, floorY + h, 0f));
            }
            line.startColor = line.endColor = Ink;
        }
    }
}
