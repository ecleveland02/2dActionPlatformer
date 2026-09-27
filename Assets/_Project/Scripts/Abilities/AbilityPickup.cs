using Margin.Core;
using Margin.FX;
using Margin.Level;
using Margin.Player;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Abilities
{
    /// <summary>
    /// A boss's reward (spec 8): a floating pen that gives an ability when touched, with a note on how to use it.
    /// The Highlighter's arena shows one (the Grapple Line) once the boss is beaten.
    /// </summary>
    public sealed class AbilityPickup : MonoBehaviour, ITickable
    {
        [SerializeField] private Ability ability = Ability.GrappleLine;
        [SerializeField] private string title = "GRAPPLE LINE";
        [SerializeField, TextArea] private string howTo =
            "I / middle click / R3: hook a ring and swing\nJump lets go   Up/Down reel   also yanks enemies to you";
        [SerializeField] private Material lineMaterial;
        [Tooltip("Frames the note stays up after picking it up.")]
        [SerializeField, Min(0)] private int noteFrames = 420;

        private static readonly Color Ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        private static readonly Color Glow = new Color(1f, 0.9f, 0.4f, 0.5f);

        private Transform pen;
        private TextMesh note;
        private bool taken;
        private int tick, noteLeft;
        private Vector3 home;

        public int TickOrder => 33;
        public bool Taken => taken;

        public void Configure(Ability give, string name, string instructions, Material material)
        {
            ability = give;
            title = name;
            howTo = instructions;
            lineMaterial = material;
        }

        private void Awake()
        {
            home = transform.position;
            Build();
        }

        private void OnEnable() => GameLoop.Register(this);
        private void OnDisable() => GameLoop.Unregister(this);

        public void Tick()
        {
            tick++;
            if (noteLeft > 0 && --noteLeft == 0) note.gameObject.SetActive(false);
            if (taken) return;

            // Bob and turn gently.
            transform.position = home + new Vector3(0f, 0.18f * Mathf.Sin(tick * 0.06f), 0f);
            pen.localRotation = Quaternion.Euler(0f, 0f, 35f + 8f * Mathf.Sin(tick * 0.045f));

            LevelDirector director = LevelDirector.Instance;
            PlayerController player = director != null ? director.Player : SceneQuery.FindFirst<PlayerController>();
            if (player == null || player.Body == null) return;
            if (Vector2.Distance(player.Body.Position, transform.position) < 1.3f) Take(player);
        }

        private void Take(PlayerController player)
        {
            taken = true;
            player.Abilities.Unlock(ability);
            pen.gameObject.SetActive(false);
            note.gameObject.SetActive(true);
            noteLeft = noteFrames;
            if (InkSplatter.Instance != null) InkSplatter.Instance.Burst(transform.position, Vector2.up, 30);
            CameraShake.Shake(0.12f);
            GameLoop.Freeze(10);
            AbilityEvents.RaiseUnlocked(ability);
        }

        private void Build()
        {
            if (pen != null) return;
            pen = new GameObject("Pen").transform;
            pen.SetParent(transform, false);
            // A fountain pen: barrel, cap band and a nib, with a soft glow behind it.
            Line(pen, "Glow", Glow, 0.7f, 4, new Vector3(0f, -0.55f), new Vector3(0f, 0.55f));
            Line(pen, "Barrel", Ink, 0.22f, 6, new Vector3(0f, -0.3f), new Vector3(0f, 0.55f));
            Line(pen, "Band", new Color32(0xE0, 0xA8, 0x10, 0xFF), 0.24f, 7, new Vector3(0f, 0.18f), new Vector3(0f, 0.26f));
            Line(pen, "Nib", Ink, 0.06f, 6, new Vector3(-0.1f, -0.3f), new Vector3(0f, -0.6f), new Vector3(0.1f, -0.3f));

            var noteObject = new GameObject("Note");
            noteObject.transform.SetParent(transform, false);
            noteObject.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            note = noteObject.AddComponent<TextMesh>();
            note.text = title + "\n" + howTo;
            note.fontSize = 48;
            note.characterSize = 0.05f;
            note.anchor = TextAnchor.MiddleCenter;
            note.alignment = TextAlignment.Center;
            note.color = Ink;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                note.font = font;
                noteObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            noteObject.SetActive(false);
        }

        private void Line(Transform parent, string lineName, Color color, float width, int order, params Vector3[] points)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = lineMaterial != null ? lineMaterial : InkMaterial.Runtime;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.numCapVertices = 3;
            line.sortingOrder = order;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = points.Length;
            line.SetPositions(points);
        }
    }
}
