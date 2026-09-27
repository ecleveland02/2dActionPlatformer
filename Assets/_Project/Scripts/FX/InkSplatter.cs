using Margin.Combat;
using Margin.Rendering;
using UnityEngine;

namespace Margin.FX
{
    /// <summary>
    /// Ink splatter on hits (spec 4.3): a burst of droplets plus a few stretched streaks from the hit point,
    /// thrown along the knockback direction. Bigger hits throw more ink. One per scene; listens to CombatEvents.
    /// </summary>
    public sealed class InkSplatter : MonoBehaviour
    {
        [SerializeField] private FeelSettings settings;

        private ParticleSystem droplets;
        private ParticleSystem streaks;
        private static readonly Color Ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);

        private FeelSettings Settings => settings != null ? settings : FeelSettings.Defaults;

        public FeelSettings FeelSettings
        {
            get => settings;
            set => settings = value;
        }

        public int LiveParticles => (droplets != null ? droplets.particleCount : 0) + (streaks != null ? streaks.particleCount : 0);

        private void Awake()
        {
            droplets = CreateSystem("Droplets", ParticleSystemRenderMode.Billboard);
            streaks = CreateSystem("Streaks", ParticleSystemRenderMode.Stretch);
        }

        private void OnEnable() => CombatEvents.Hit += OnHit;
        private void OnDisable() => CombatEvents.Hit -= OnHit;

        private void OnHit(HitInfo hit, IHitReceiver target)
        {
            Vector2 dir = hit.Knockback.sqrMagnitude > 0.0001f ? hit.Knockback.normalized : Vector2.right;
            Burst(hit.Point, dir, hit.Damage);
        }

        /// <summary>Throws ink from a point along a direction. Also usable for other effects (e.g. parries).</summary>
        public void Burst(Vector2 point, Vector2 direction, int damage)
        {
            FeelSettings s = Settings;
            int count = FeelMath.SplatterCount(s.splatterBase, s.splatterPerDamage, damage);
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            for (int i = 0; i < count; i++)
            {
                bool streak = i % 4 == 0;   // every 4th particle is a streak
                float angle = (baseAngle + Random.Range(-s.splatterSpread, s.splatterSpread)) * Mathf.Deg2Rad;
                float speed = Random.Range(s.splatterSpeed.x, s.splatterSpeed.y) * (streak ? 1.3f : 1f);
                var emit = new ParticleSystem.EmitParams
                {
                    position = point,
                    velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * speed,
                    startSize = Random.Range(s.splatterSize.x, s.splatterSize.y) * (streak ? 0.7f : 1f),
                    startLifetime = Random.Range(s.splatterLifetime.x, s.splatterLifetime.y),
                    startColor = Ink,
                    applyShapeToPosition = false,
                };
                (streak ? streaks : droplets).Emit(emit, 1);
            }
        }

        private ParticleSystem CreateSystem(string name, ParticleSystemRenderMode mode)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 1000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = Settings.splatterGravity;
            main.startSpeed = 0f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;          // particles only come from Burst()
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            // Droplets shrink as they fly, so the burst reads as ink flicked off the blade.
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = InkMaterial.Runtime;
            renderer.renderMode = mode;
            renderer.sortingOrder = 30;
            if (mode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = 0.05f;
                renderer.lengthScale = 2f;
            }

            ps.Play();
            return ps;
        }
    }
}
