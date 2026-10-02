using System.Collections.Generic;
using Margin.Core;
using Margin.Level;
using Margin.Player;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Combat
{
    /// <summary>
    /// A shot fired by an enemy (the Tack Turret's tacks): flies in a straight line, hits the player once using its
    /// AttackData (damage, knockback, hitstun; parryable or not), and stops at walls and floors. Drawn as a thumbtack
    /// pointing along its flight. Cleared when the player changes room or respawns.
    /// </summary>
    public enum ProjectileLook { Tack, Staple }

    public sealed class EnemyProjectile : MonoBehaviour, ITickable, IHitboxSource
    {
        private static readonly Color Ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        private static readonly Color Head = new Color32(0xC8, 0x3C, 0x32, 0xFF);
        private static readonly Color Steel = new Color32(0x5C, 0x60, 0x68, 0xFF);

        private Component owner;
        private AttackData attack;
        private CombatSettings settings;
        private Vector2 position, velocity;
        private LayerMask solid;
        private int lifeLeft;
        private float size;
        private ProjectileLook look;
        private readonly HashSet<IHitReceiver> hit = new HashSet<IHitReceiver>();
        private readonly List<AabbBox> boxes = new List<AabbBox>();
        private LineRenderer pin, head;

        public int TickOrder => 24;
        public Faction Faction => Faction.Enemy;
        public IReadOnlyList<AabbBox> ActiveHitboxes => boxes;
        public Vector2 Position => position;

        public static EnemyProjectile Spawn(Component owner, AttackData attack, CombatSettings settings, Vector2 from,
                                            Vector2 velocity, LayerMask solid, int lifetimeFrames, float size = 0.35f,
                                            ProjectileLook look = ProjectileLook.Tack)
        {
            var go = new GameObject(look == ProjectileLook.Staple ? "Staple" : "Tack");
            var p = go.AddComponent<EnemyProjectile>();
            p.owner = owner;
            p.attack = attack;
            p.settings = settings;
            p.position = from;
            p.velocity = velocity;
            p.solid = solid;
            p.lifeLeft = Mathf.Max(1, lifetimeFrames);
            p.size = size;
            p.look = look;
            p.Build();
            p.Draw();
            return p;
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
            Vector2 step = velocity * GameTime.TickDelta;
            RaycastHit2D wall = UnityEngine.Physics2D.Raycast(position, step.normalized, step.magnitude, solid);
            position = wall.collider != null ? wall.point : position + step;

            boxes.Clear();
            var box = new AabbBox(position.x, position.y, size, size);
            boxes.Add(box);
            int facing = velocity.x < 0f ? -1 : 1;
            int hits = HitResolver.Resolve(owner != null ? owner : this, Faction.Enemy, attack, box, facing, hit,
                                           settings != null ? settings : CombatSettings.Defaults);
            Draw();

            if (hits > 0 || hit.Count > 0 || wall.collider != null || --lifeLeft <= 0)
            {
                if (wall.collider != null && FX.InkSplatter.Instance != null)
                    FX.InkSplatter.Instance.Burst(position, -step.normalized, 3);
                Destroy(gameObject);
            }
        }

        private void Build()
        {
            pin = Line("Pin", look == ProjectileLook.Staple ? Steel : Ink, 0.06f, 13);
            head = Line("Head", Head, 0.22f, 14);
            head.enabled = look == ProjectileLook.Tack;
        }

        private LineRenderer Line(string lineName, Color color, float width, int order)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = InkMaterial.Runtime;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.numCapVertices = 3;
            line.sortingOrder = order;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        /// <summary>A thumbtack: a red round head with the steel pin pointing the way it flies.</summary>
        private void Draw()
        {
            if (pin == null) return;
            Vector2 dir = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector2.right;
            if (look == ProjectileLook.Staple)
            {
                // A staple: the crown across the back, both legs pointing the way it flies.
                Vector2 side = new Vector2(-dir.y, dir.x) * size * 0.5f;
                Vector2 crown = position - dir * size * 0.5f, legs = position + dir * size * 0.5f;
                pin.positionCount = 4;
                pin.SetPosition(0, legs + side);
                pin.SetPosition(1, crown + side);
                pin.SetPosition(2, crown - side);
                pin.SetPosition(3, legs - side);
                return;
            }
            Vector3 tip = position + dir * 0.22f, back = position - dir * 0.12f;
            pin.positionCount = 2;
            pin.SetPosition(0, back);
            pin.SetPosition(1, tip);
            head.positionCount = 2;
            head.SetPosition(0, position - dir * 0.2f);
            head.SetPosition(1, position - dir * 0.1f);
        }
    }
}
