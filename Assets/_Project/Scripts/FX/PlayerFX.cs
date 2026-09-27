using Margin.Core;
using Margin.Player;
using Margin.Rendering;
using UnityEngine;

namespace Margin.FX
{
    /// <summary>
    /// The player's game-feel effects, updated every gameplay tick right after the pose (TickOrder 11):
    ///   - smear crescents on the fastest frames of attacks whose AttackData has Smear on,
    ///   - the blade tip trail while attacking,
    ///   - afterimages while dashing.
    /// Everything freezes during the player's hitstop, so a hit holds its smear like a freeze-frame.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerFX : MonoBehaviour, ITickable
    {
        [SerializeField] private FeelSettings settings;

        private static readonly Color Ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        private static readonly Color Ghost = new Color32(0x7A, 0x7A, 0x7A, 0xFF);

        private PlayerController player;
        private StickFigureRig rig;
        private WeaponLine blade;
        private float previousAngle;
        private bool hasPrevious;
        private int attackSerial = -1;

        public int TickOrder => 11;
        public SmearRenderer Smear { get; private set; }
        public AfterimagePool Afterimages { get; private set; }
        public WeaponLine Blade => blade;

        public FeelSettings FeelSettings
        {
            get => settings;
            set => settings = value;
        }

        private FeelSettings Settings => settings != null ? settings : FeelSettings.Defaults;

        private void Awake() => player = GetComponent<PlayerController>();
        private void OnEnable() => GameLoop.Register(this);
        private void OnDisable() => GameLoop.Unregister(this);

        private void OnDestroy()
        {
            if (Smear != null) Destroy(Smear.gameObject);
            Afterimages?.Destroy();
        }

        /// <summary>Finds the rig and blade and builds the effect objects. Runs automatically on the first tick.</summary>
        public void Setup()
        {
            rig = GetComponentInChildren<StickFigureRig>();
            blade = GetComponentInChildren<WeaponLine>();
            FeelSettings s = Settings;
            int order = rig != null && rig.Proportions != null ? rig.Proportions.sortingOrder : 10;
            Material material = rig != null && rig.LineMaterial != null ? rig.LineMaterial : InkMaterial.Runtime;

            if (Smear == null) Smear = SmearRenderer.Create(order + 3);
            if (Afterimages == null)
                Afterimages = new AfterimagePool(s.afterimageCount, 14, s.afterimageFadeFrames, s.afterimageAlpha, Ghost, material, order - 3);
            if (blade != null) blade.ConfigureTrail(s.trailTime, s.trailWidth, Ink, order + 2);
        }

        public void Tick()
        {
            if (Smear == null) Setup();
            if (player.InHitstop) return;

            Smear.Tick();
            Afterimages.Tick();

            AttackState attack = player.CurrentState as AttackState;
            if (blade != null) blade.TrailEmitting = attack != null;
            UpdateSmear(attack);

            if (player.CurrentState is DashState) UpdateAfterimages();
        }

        private void UpdateSmear(AttackState attack)
        {
            if (attack == null || !attack.Attack.smear || rig == null || blade == null || !rig.IsBuilt)
            {
                hasPrevious = false;
                return;
            }
            if (player.Combat.AttackSerial != attackSerial)
            {
                attackSerial = player.Combat.AttackSerial;
                hasPrevious = false;   // a new attack starts a new swing
            }

            // Blade tip from the pose applied this tick (the blade line itself only updates when rendering).
            Vector3 shoulder = rig.ShoulderPosition;
            Vector3 hand = rig.HandPosition(front: true);
            Vector3 along = (hand - rig.Pivot(PoseJoint.ElbowFront).position).normalized;
            Vector3 tip = hand + along * blade.Length * Mathf.Abs(rig.transform.lossyScale.y);

            Vector3 arm = tip - shoulder;
            float angle = Mathf.Atan2(arm.y, arm.x) * Mathf.Rad2Deg;

            // Only the swing itself smears: the last 2 startup frames (where the clip accelerates into the strike)
            // through the active frames. The blend into the wind-up is anticipation, not a cut.
            int frame = attack.Frame;
            bool swinging = frame >= attack.Attack.startupFrames - 1 && frame <= attack.Attack.Timing.LastActiveFrame;

            FeelSettings s = Settings;
            if (swinging && hasPrevious && Mathf.Abs(FeelMath.DeltaAngle(previousAngle, angle)) >= s.smearMinAngle)
            {
                Color c = Ink;
                c.a = s.smearAlpha;
                Smear.Show(shoulder, previousAngle, angle, arm.magnitude, s.smearThickness, c, s.smearFrames);
            }
            previousAngle = angle;
            hasPrevious = true;
        }

        private void UpdateAfterimages()
        {
            if (rig == null || !rig.IsBuilt) return;
            // First dash frame, then every interval frames.
            if ((player.FramesInState - 1) % Settings.afterimageInterval != 0) return;

            LineRenderer[] lines = rig.Lines;
            LineRenderer[] weapon = blade != null ? blade.Lines : new LineRenderer[0];
            var all = new LineRenderer[lines.Length + weapon.Length];
            lines.CopyTo(all, 0);
            weapon.CopyTo(all, lines.Length);
            Afterimages.Spawn(all);
        }
    }
}
