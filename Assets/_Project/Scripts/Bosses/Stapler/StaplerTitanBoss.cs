using System.Collections.Generic;
using Margin.Audio;
using Margin.Combat;
using Margin.Core;
using Margin.FX;
using Margin.Level;
using Margin.Player;
using UnityEngine;

namespace Margin.Bosses
{
    /// <summary>
    /// Boss 2, The Stapler Titan (spec 10: tests positioning and punishing openings). A giant desk stapler.
    ///
    /// Phase 1:
    ///   Chomp        the jaw creaks open with a glint (telegraph), then snaps shut as it lunges. Parry it.
    ///   Hop Slam     crouches, flashes red, leaps to where you stand and slams down: two shockwaves run along the
    ///                floor both ways. Can't be parried: get out from under it, jump the waves. The landing jams
    ///                it (dizzy) for a long punish window.
    ///   Staple Shot  flashes red, then fires staples one after another, each aimed at you. Move or dash through.
    /// Phase 2 (at 50%): staples pin the moving platforms in place and the high ones are torn out
    /// (<see cref="StaplePlatform"/>; the doomed ones flicker first).
    ///   Staple Rain  the jaw opens to the sky and dashed lanes appear (one on you); staples fall down them, one
    ///                lane after another. Can't be parried: step out of the lanes or shelter under a stapled platform.
    ///   Chomp, Hop Slam (taller waves) and Staple Shot as before.
    /// Frame data (startup = telegraph, 20+ frames) and damage live in each move's AttackData; speeds in StaplerData.
    /// </summary>
    public sealed class StaplerTitanBoss : BossBase
    {
        public const string Chomp = "Chomp", HopSlam = "HopSlam", StapleShot = "StapleShot", StapleRain = "StapleRain";

        [SerializeField] private StaplerVisual visual;
        [Tooltip("The shockwaves' hit (Hop Slam's own AttackData is the landing itself).")]
        [SerializeField] private AttackData waveAttack;

        private static StaplerData fallback;

        // Pose, set by the moves each tick and drawn by UpdateVisual.
        private float jaw, squash = 1f, stride;
        private bool sparkle;
        private HighlighterEyes eyes = HighlighterEyes.Closed;

        // Move state.
        private int recoverFrom, shotsFired;
        private bool hopping;
        private float hopGravity;
        private readonly List<float> lanes = new List<float>();
        private int dropsFired;
        private uint seed = 2463534242u;
        private readonly List<StaplePlatform> platforms = new List<StaplePlatform>();
        private readonly List<Object> spawned = new List<Object>();

        private StaplerData D
        {
            get
            {
                if (data is StaplerData d) return d;
                if (fallback == null)
                {
                    fallback = ScriptableObject.CreateInstance<StaplerData>();
                    fallback.hideFlags = HideFlags.DontSave;
                }
                return fallback;
            }
        }

        public void ConfigureLook(StaplerVisual look, AttackData shockwave)
        {
            visual = look;
            waveAttack = shockwave;
        }

        /// <summary>The landing spot's arc is in flight (tests check it).</summary>
        public bool Hopping => hopping;
        public IReadOnlyList<float> RainLanes => lanes;

        // ---------------- arena geometry ----------------

        private float BodyHalf => Body.Size.y * 0.5f;
        private float FeetY => Position.y - BodyHalf;
        private float FloorY => Home.y - BodyHalf;

        private Rect Arena
        {
            get
            {
                Room room = Room.Of(this);
                return room != null ? room.WorldBounds : new Rect(Home.x - 18f, FloorY - 2f, 36f, 18f);
            }
        }

        private float Left => Arena.xMin + 2.2f;
        private float Right => Arena.xMax - 2.2f;
        private float CenterX => Arena.center.x;

        private LayerMask WallMask => Body.Data != null ? Body.Data.solidMask : (LayerMask)(1 << 6);
        private LayerMask FloorMask => Body.Data != null ? (LayerMask)(Body.Data.solidMask | Body.Data.oneWayMask) : (LayerMask)((1 << 6) | (1 << 7));

        // ---------------- intro ----------------

        protected override void OnDormantTick()
        {
            eyes = HighlighterEyes.Closed;
            jaw = 0f;
            squash = 1f + 0.02f * Mathf.Sin(ModeFrames * 0.05f);   // asleep, breathing
        }

        protected override void OnIntroStart(bool repeat)
        {
            FindPlatforms();
            Velocity = Vector2.zero;
        }

        protected override void OnIntroTick()
        {
            float t = ModeFrames / (float)Mathf.Max(1, ModeLength);
            eyes = t < 0.2f ? HighlighterEyes.Closed : HighlighterEyes.Angry;
            // Wakes up with two snaps of the jaw.
            jaw = t < 0.25f ? 0f : 35f * Mathf.Abs(Mathf.Sin((t - 0.25f) * Mathf.PI * 4f));
            if (ModeFrames == Mathf.Max(1, ModeLength / 2)) CameraShake.Shake(0.1f);

            // Title card, only on the full-length (first) intro.
            if (Data != null && ModeLength >= 60 && visual != null)
            {
                float a = Mathf.Clamp01((t - 0.3f) * 5f) * Mathf.Clamp01((1f - t) * 6f);
                visual.ShowTitle(Data.bossName, new Vector2(CenterX, Arena.yMax - 3.5f), a);
            }
        }

        private void FindPlatforms()
        {
            platforms.Clear();
            Room room = Room.Of(this);
            if (room != null) platforms.AddRange(room.GetComponentsInChildren<StaplePlatform>(true));
            else platforms.AddRange(SceneQuery.FindAll<StaplePlatform>());   // no room (tests, a loose scene)
        }

        // ---------------- rest (the openings) ----------------

        protected override void OnRestStart()
        {
            sparkle = false;
            if (visual != null) visual.ShowTitle("", Vector2.zero, 0f);
        }

        protected override void OnRestTick()
        {
            jaw = Mathf.MoveTowards(jaw, 0f, 6f);
            squash = Mathf.MoveTowards(squash, 1f + 0.03f * Mathf.Sin(ModeFrames * 0.2f), 0.05f);
            eyes = HighlighterEyes.Angry;
            if (Target == null) return;
            // Keep a middling distance: stomp in when far, back off when crowded.
            float d = DistanceToTarget;
            int walk = d > 7f ? Facing : d < 3f ? -Facing : 0;
            Velocity.x = Mathf.MoveTowards(Velocity.x, walk * Data.walkSpeed, 0.6f);
        }

        // ---------------- moves ----------------

        protected override string TellSound(BossMoveEntry move)
        {
            if (!string.IsNullOrEmpty(move.tellSound)) return move.tellSound;
            switch (move.move)
            {
                case Chomp: return "boss_swipe_tell";
                case HopSlam: return "boss_dash_tell";
                case StapleShot: return "boss_cap_tell";
                case StapleRain: return "boss_drip_tell";
                default: return "";
            }
        }

        protected override bool CanUse(BossMoveEntry move)
        {
            // Never two of its own attack types out at once (spec 10): no new move while waves or staples still fly.
            spawned.RemoveAll(o => o == null);
            if (spawned.Count > 0 && move.move != Chomp) return false;
            if (move.move == StapleRain) return PhaseIndex >= 1;
            return true;
        }

        protected override void OnMoveStart(BossMoveEntry move)
        {
            recoverFrom = 0;
            shotsFired = 0;
            dropsFired = 0;
            hopping = false;
            sparkle = false;
            Velocity.x = 0f;
            if (move.move == StapleRain)
            {
                float px = Target != null ? Target.Body.Position.x : CenterX;
                lanes.Clear();
                lanes.AddRange(StaplerMath.RainLanes(px, Arena.xMin + 1f, Arena.xMax - 1f, D.rainCount, D.rainSpacing,
                                                     NextSeed()));
            }
        }

        protected override bool TickMove(BossMoveEntry move, int f)
        {
            AttackData a = move.attack;
            int s = a.startupFrames, act = a.activeFrames, rec = a.recoveryFrames;
            switch (move.move)
            {
                case Chomp:
                    if (f <= s)
                    {
                        // Creaks open with a glint on the nose: parry when it snaps.
                        float t = f / (float)s;
                        jaw = 70f * (1f - (1f - t) * (1f - t));
                        squash = 1f - 0.08f * t;
                        sparkle = f > s / 3;
                        Velocity.x = 0f;
                    }
                    else if (f <= s + act)
                    {
                        sparkle = false;
                        jaw = Mathf.Lerp(70f, -3f, (f - s) / (float)act);
                        squash = 1.06f;
                        Velocity.x = Facing * D.chompLunge;
                        foreach (HitboxWindow w in a.WindowsAt(f))
                            foreach (HitboxShape shape in w.boxes)
                                Strike(a, HitboxMath.ToWorld(Position.x, Position.y, Facing, shape.offset.x, shape.offset.y,
                                                             shape.size.x, shape.size.y));
                        if (f == s + act) CameraShake.Shake(0.08f);
                    }
                    else
                    {
                        // Snapped shut on nothing: the jaw bounces and it shakes itself loose.
                        int k = f - s - act;
                        jaw = Mathf.Max(0f, 10f * Mathf.Sin(k * 0.6f) * (1f - k / (float)Mathf.Max(1, rec)));
                        squash = Mathf.MoveTowards(squash, 1f, 0.02f);
                        Velocity.x = Mathf.MoveTowards(Velocity.x, 0f, 1.2f);
                    }
                    return f < a.TotalFrames;

                case HopSlam:
                    if (f <= s)
                    {
                        // Crouches (red flash comes from BossBase: unparryable).
                        Velocity.x = 0f;
                        float t = f / (float)s;
                        squash = 1f - 0.25f * t;
                        jaw = 12f * t;
                        return true;
                    }
                    if (f == s + 1) Leap();
                    if (recoverFrom == 0)
                    {
                        squash = Mathf.MoveTowards(squash, Velocity.y > 0f ? 1.12f : 0.95f, 0.04f);
                        jaw = 18f;
                        if (Velocity.y < 0f) Strike(a, new AabbBox(Position.x, FeetY + 0.6f, Body.Size.x + 0.2f, 1.2f));
                        bool landed = f > s + 2 && Body.Collisions.Grounded && Velocity.y <= 0f;
                        if (landed || f - s > act + 60) Land(move);
                        return true;
                    }
                    else
                    {
                        // Jammed: a big opening.
                        int k = f - recoverFrom;
                        eyes = k < rec - 12 ? HighlighterEyes.Dizzy : HighlighterEyes.Angry;
                        squash = k < 6 ? 0.78f : Mathf.MoveTowards(squash, 1f, 0.01f);
                        jaw = 0f;
                        Velocity.x = 0f;
                        return k < rec;
                    }

                case StapleShot:
                    Velocity.x = 0f;
                    if (f <= s)
                    {
                        float t = f / (float)s;
                        jaw = 28f * t;
                        squash = 1f - 0.05f * t;
                        return true;
                    }
                    int since = f - s - 1;
                    if (since >= 0 && since % Mathf.Max(1, D.shotInterval) == 0 && shotsFired < D.shotCount)
                    {
                        FireStaple(a);
                        shotsFired++;
                        jaw = 4f;   // a snap per staple
                    }
                    else jaw = Mathf.MoveTowards(jaw, 28f, 6f);
                    if (f > s + act) jaw = Mathf.MoveTowards(jaw, 0f, 3f);
                    return f < a.TotalFrames;

                case StapleRain:
                    Velocity.x = 0f;
                    if (f <= s)
                    {
                        float t = f / (float)s;
                        jaw = 100f * (1f - (1f - t) * (1f - t));
                        squash = 1f + 0.06f * Mathf.Sin(f * 0.9f);
                        DrawLanes(t, 0);
                        return true;
                    }
                    int sinceRain = f - s - 1;
                    if (sinceRain >= 0 && sinceRain % Mathf.Max(1, D.rainInterval) == 0 && dropsFired < lanes.Count)
                    {
                        DropStaple(a, lanes[dropsFired]);
                        dropsFired++;
                        squash = 0.92f;
                    }
                    else squash = Mathf.MoveTowards(squash, 1f, 0.02f);
                    DrawLanes(1f, dropsFired);
                    if (f > s + act) jaw = Mathf.MoveTowards(jaw, 0f, 4f);
                    return f < a.TotalFrames;
            }
            return false;
        }

        protected override void OnMoveEnd(BossMoveEntry move)
        {
            sparkle = false;
            hopping = false;
            if (visual != null)
            {
                visual.BeginDashes();
                visual.EndDashes();
            }
            if (Mode == BossMode.Attacking) Velocity.x = 0f;
        }

        private void Leap()
        {
            float px = Target != null ? Target.Body.Position.x : Position.x + Facing * 6f;
            StaplerMath.HopVelocity(Position.x, px, Left, Right, D.hopMaxDistance, D.hopAirFrames, D.hopHeight,
                                    GameTime.TickDelta, out float vx, out float vy, out hopGravity, out _);
            Velocity = new Vector2(vx, vy);
            hopping = true;
            squash = 1.15f;
            Sfx.Play("jump");
        }

        private void Land(BossMoveEntry move)
        {
            hopping = false;
            recoverFrom = MoveFrame;
            Velocity = Vector2.zero;
            squash = 0.7f;
            CameraShake.Shake(0.22f);
            Sfx.Play("hit_heavy");
            if (InkSplatter.Instance != null) InkSplatter.Instance.Burst(new Vector2(Position.x, FeetY + 0.1f), Vector2.up, 16);
            if (waveAttack == null) return;
            float height = PhaseIndex >= 1 ? D.waveHeightPhase2 : D.waveHeight;
            float half = Body.Size.x * 0.5f;
            for (int side = -1; side <= 1; side += 2)
                spawned.Add(ShockwaveProjectile.Spawn(this, waveAttack, Settings, Position.x + side * half, FeetY, side,
                                                      D.waveSpeed, height, D.waveLifetimeFrames, WallMask, FloorMask).gameObject);
        }

        private void FireStaple(AttackData attack)
        {
            Vector2 from = visual != null ? visual.Nose : Position + new Vector2(Facing * 1.6f, 0.3f);
            Vector2 to = Target != null ? Target.Body.Position : from + new Vector2(Facing, 0f);
            Vector2 dir = (to - from).sqrMagnitude > 0.01f ? (to - from).normalized : new Vector2(Facing, 0f);
            float range = Arena.width + 4f;
            int life = Mathf.CeilToInt(range / D.shotSpeed / GameTime.TickDelta);
            spawned.Add(EnemyProjectile.Spawn(this, attack, Settings, from, dir * D.shotSpeed, WallMask, life, 0.45f,
                                              ProjectileLook.Staple).gameObject);
            Sfx.Play("swing_light");
        }

        private void DropStaple(AttackData attack, float x)
        {
            var from = new Vector2(x, Arena.yMax - 0.3f);
            int life = Mathf.CeilToInt((Arena.height + 2f) / D.rainSpeed / GameTime.TickDelta);
            // Falling staples stop on platforms too: a stapled platform is a roof.
            spawned.Add(EnemyProjectile.Spawn(this, attack, Settings, from, new Vector2(0f, -D.rainSpeed), FloorMask, life, 0.5f,
                                              ProjectileLook.Staple).gameObject);
        }

        private uint NextSeed()
        {
            seed = seed * 1664525u + 1013904223u;
            return seed;
        }

        private void DrawLanes(float alpha, int firstLane)
        {
            if (visual == null) return;
            visual.BeginDashes();
            for (int i = firstLane; i < lanes.Count; i++)
                visual.Dashed(new Vector2(lanes[i], Arena.yMax), new Vector2(lanes[i], FloorY), alpha);
            visual.EndDashes();
        }

        // ---------------- stagger, phase change, defeat ----------------

        protected override void OnStaggerStart()
        {
            Velocity = Vector2.zero;
            sparkle = false;
            hopping = false;
            eyes = HighlighterEyes.Dizzy;
        }

        protected override void OnStaggerTick()
        {
            eyes = HighlighterEyes.Dizzy;
            jaw = Mathf.MoveTowards(jaw, 8f + 6f * Mathf.Sin(ModeFrames * 0.3f), 6f);
            squash = Mathf.MoveTowards(squash, 0.92f, 0.02f);
            Velocity.x = 0f;
        }

        protected override void OnPhaseShiftStart(int phase, bool repeat)
        {
            sparkle = false;
            hopping = false;
            Velocity = Vector2.zero;
            if (platforms.Count == 0) FindPlatforms();
            CameraShake.Shake(0.2f);
            Sfx.Play("boss_phase");
        }

        protected override void OnPhaseShiftTick()
        {
            eyes = HighlighterEyes.Angry;
            Velocity.x = 0f;
            float t = ModeFrames / (float)Mathf.Max(1, ModeLength);
            // Jaw to the sky, snapping out staples at the ceiling.
            jaw = 95f + 10f * Mathf.Sin(ModeFrames * 0.8f);
            squash = 1f + 0.05f * Mathf.Sin(ModeFrames * 0.7f);

            int pinFrame = Mathf.Max(1, Mathf.RoundToInt(ModeLength * D.pinAt));
            int removeFrame = Mathf.Max(pinFrame + 1, Mathf.RoundToInt(ModeLength * D.removeAt));
            foreach (StaplePlatform p in platforms)
            {
                if (p == null) continue;
                if (p.KeepInPhase2)
                {
                    if (ModeFrames == pinFrame) PinPlatform(p);
                }
                else if (ModeFrames >= pinFrame && ModeFrames < removeFrame) p.Warn(ModeFrames);
                else if (ModeFrames == removeFrame) TearOut(p);
            }
            if (t > 0.9f) jaw = Mathf.MoveTowards(jaw, 0f, 10f);
        }

        private static void PinPlatform(StaplePlatform p)
        {
            p.Pin();
            Sfx.Play("land");
            CameraShake.Shake(0.08f);
            if (InkSplatter.Instance != null) InkSplatter.Instance.Burst(p.transform.position, Vector2.up, 6);
        }

        private static void TearOut(StaplePlatform p)
        {
            p.Remove();
            Sfx.Play("page_turn");
            CameraShake.Shake(0.12f);
            if (InkSplatter.Instance != null) InkSplatter.Instance.Burst(p.transform.position, Vector2.up, 12);
        }

        protected override void OnDefeatStart()
        {
            Velocity = Vector2.zero;
            sparkle = false;
            hopping = false;
            ClearSpawned();
            if (visual != null)
            {
                visual.BeginDashes();
                visual.EndDashes();
            }
            CameraShake.Shake(0.3f);
            Sfx.Play("boss_defeat");
        }

        protected override void OnDefeatTick()
        {
            eyes = HighlighterEyes.Crossed;
            Velocity.x = 0f;
            // The jaw flops open and staples spill out.
            jaw = Mathf.MoveTowards(jaw, 60f, 1.5f);
            squash = 1f + 0.03f * Mathf.Sin(ModeFrames * 1.1f);
            if (ModeFrames % 25 == 0 && InkSplatter.Instance != null)
                InkSplatter.Instance.Burst(Position + new Vector2(Random.Range(-1f, 1f), Random.Range(0f, 1f)), Vector2.up, 8);
        }

        protected override void OnVanish()
        {
            if (InkSplatter.Instance != null) InkSplatter.Instance.Burst(Position, Vector2.up, 40);
            CameraShake.Shake(0.3f);
            if (visual != null) visual.SetVisible(false);
        }

        protected override void OnSkippedToBeaten()
        {
            ClearSpawned();
            if (visual != null)
            {
                visual.SetVisible(false);
                visual.ShowTitle("", Vector2.zero, 0f);
                visual.BeginDashes();
                visual.EndDashes();
            }
        }

        protected override void OnReset()
        {
            ClearSpawned();
            jaw = 0f;
            squash = 1f;
            stride = 0f;
            sparkle = false;
            hopping = false;
            eyes = HighlighterEyes.Closed;
            lanes.Clear();
            seed ^= (uint)System.Environment.TickCount;   // different lanes each attempt
            if (platforms.Count == 0) FindPlatforms();
            foreach (StaplePlatform p in platforms)
                if (p != null) p.Restore();
            if (visual != null)
            {
                visual.SetVisible(true);
                visual.ShowTitle("", Vector2.zero, 0f);
                visual.BeginDashes();
                visual.EndDashes();
            }
        }

        private void ClearSpawned()
        {
            foreach (Object o in spawned)
                if (o != null) Destroy(o);
            spawned.Clear();
        }

        // ---------------- every tick ----------------

        protected override void OnAnyTick()
        {
            if (Mode == BossMode.Gone) return;
            if (hopping)
            {
                // Its own arc (StaplerMath), not the player's heavy fall gravity.
                Velocity.y = MovementMath.VerticalStep(Velocity.y, hopGravity, 60f, out float dy);
                Body.Move(new Vector2(Velocity.x * GameTime.TickDelta, dy));
                if (Body.Collisions.HitWallLeft || Body.Collisions.HitWallRight) Velocity.x = 0f;
                if (Body.Collisions.HitCeiling && Velocity.y > 0f) Velocity.y = 0f;
            }
            else MoveGrounded();
            stride = Mathf.Abs(Velocity.x) > 0.3f && Body.Collisions.Grounded ? Mathf.Sin(ModeFrames * 0.35f) : 0f;
        }

        protected override void UpdateVisual()
        {
            if (visual == null) return;
            Color? tint = TelegraphFlash ? (MoveFrame / 3 % 2 == 0 ? new Color(0.85f, 0.1f, 0.1f) : new Color(0.45f, 0.05f, 0.05f)) : (Color?)null;
            visual.Pose(Facing, jaw, squash, stride, eyes, sparkle, tint, HurtFlash);
        }
    }
}
