using System.Collections.Generic;
using Margin.Combat;
using Margin.Core;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Bosses
{
    /// <summary>
    /// The Highlighter's thrown cap: flies in an arc at where the player was. Parry it (spec 6.5) and it shoots back
    /// into the boss for big damage and a stagger. A missed cap lies on the floor, then floats home harmlessly.
    /// Ticks with the GameLoop and draws itself with LineRenderers.
    /// </summary>
    public sealed class HighlighterCap : MonoBehaviour, ITickable, IHitboxSource, IParryable
    {
        private enum State { Flying, Resting, Returning, Reflected }

        private static readonly Color Ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        private static readonly Color Marker = new Color32(0xE9, 0xC9, 0x30, 0xFF);
        private const float Width = 0.55f, Length = 0.75f;

        private HighlighterBoss owner;
        private AttackData attack;
        private CombatSettings settings;
        private Vector2 position, velocity;
        private float gravity, spin, restY;
        private State state;
        private int stateFrames;
        private readonly HashSet<IHitReceiver> hit = new HashSet<IHitReceiver>();
        private readonly List<AabbBox> boxes = new List<AabbBox>();
        private LineRenderer fill, outline;

        public int TickOrder => 23;
        public Vector2 Position => position;
        public bool Reflected => state == State.Reflected;

        public static HighlighterCap Spawn(HighlighterBoss owner, AttackData attack, CombatSettings settings, Vector2 from,
                                           Vector2 velocity, float gravity, float restY, Material material)
        {
            var go = new GameObject("Highlighter Cap");
            var cap = go.AddComponent<HighlighterCap>();
            cap.owner = owner;
            cap.attack = attack;
            cap.settings = settings;
            cap.position = from;
            cap.velocity = velocity;
            cap.gravity = gravity;
            cap.restY = restY;
            cap.Build(material);
            cap.Draw();
            return cap;
        }

        private void OnEnable()
        {
            GameLoop.Register(this);
            HitboxSources.Register(this);
        }

        private void OnDisable()
        {
            GameLoop.Unregister(this);
            HitboxSources.Unregister(this);
        }

        public Faction Faction => state == State.Reflected ? Faction.Player : Faction.Enemy;
        public IReadOnlyList<AabbBox> ActiveHitboxes => boxes;
        private AabbBox Box => new AabbBox(position.x, position.y, Width + 0.15f, Length);

        public void Tick()
        {
            stateFrames++;
            boxes.Clear();
            float dt = GameTime.TickDelta;
            switch (state)
            {
                case State.Flying:
                    velocity.y -= gravity * dt;
                    position += velocity * dt;
                    spin += 14f * Mathf.Sign(velocity.x == 0f ? 1f : velocity.x);
                    boxes.Add(Box);
                    HitResolver.Resolve(this, Faction.Enemy, attack, Box, velocity.x < 0f ? -1 : 1, hit,
                                        settings != null ? settings : CombatSettings.Defaults);
                    if (position.y <= restY && velocity.y < 0f)
                    {
                        position.y = restY;
                        Enter(State.Resting);
                    }
                    break;
                case State.Resting:
                    spin = Mathf.MoveTowards(spin, 90f, 20f);
                    if (owner == null || stateFrames >= owner.CapReturnDelay) Enter(State.Returning);
                    break;
                case State.Returning:
                    if (owner == null)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    Vector2 home = owner.CapHome;
                    position = Vector2.MoveTowards(position, home, 16f * dt);
                    spin = Mathf.MoveTowards(spin, 0f, 25f);
                    if ((position - home).sqrMagnitude < 0.05f || stateFrames > 180) Home(false);
                    break;
                case State.Reflected:
                    if (owner == null)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    Vector2 target = owner.Position;
                    position = Vector2.MoveTowards(position, target, owner.CapReflectSpeed * dt);
                    spin += 30f;
                    boxes.Add(Box);
                    if ((position - target).sqrMagnitude < 1.2f || stateFrames > 90) Home(true);
                    break;
            }
            Draw();
        }

        /// <summary>Parried by the player: straight back at the boss.</summary>
        public void OnParried(in HitInfo parry, int staggerFrames)
        {
            if (state == State.Flying) Enter(State.Reflected);
        }

        /// <summary>Phase change or reset: stop being dangerous and go home.</summary>
        public void Recall()
        {
            if (state != State.Returning) Enter(State.Returning);
        }

        private void Enter(State next)
        {
            state = next;
            stateFrames = 0;
        }

        private void Home(bool reflected)
        {
            if (owner != null)
            {
                if (reflected) owner.CapReflected(this);
                else owner.CapReturned(this);
            }
            Destroy(gameObject);
        }

        private void Build(Material material)
        {
            fill = Line("Fill", Marker, Width, 12, material);
            outline = Line("Outline", Ink, 0.06f, 13, material);
            outline.loop = true;
        }

        private LineRenderer Line(string lineName, Color color, float width, int order, Material material)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = material != null ? material : InkMaterial.Runtime;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.sortingOrder = order;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        /// <summary>A capsule-shaped cap turned by its spin, with a clip line along one side.</summary>
        private void Draw()
        {
            if (fill == null) return;
            Quaternion r = Quaternion.Euler(0f, 0f, spin);
            Vector3 p = position;
            float h = Length * 0.5f - Width * 0.25f;
            fill.positionCount = 2;
            fill.SetPosition(0, p + r * new Vector3(0f, -h, 0f));
            fill.SetPosition(1, p + r * new Vector3(0f, h, 0f));

            var pts = new List<Vector3>();
            float w = Width * 0.5f + 0.03f, l = Length * 0.5f + 0.03f;
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.PI * i / 8f;
                pts.Add(new Vector3(Mathf.Cos(a) * w, l - w + Mathf.Sin(a) * w, 0f));
            }
            pts.Add(new Vector3(-w, -l, 0f));
            pts.Add(new Vector3(w, -l, 0f));
            outline.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++) outline.SetPosition(i, p + r * pts[i]);
            Color c = state == State.Reflected ? new Color(0.2f, 0.2f, 0.2f) : Ink;
            outline.startColor = outline.endColor = c;
        }
    }
}
