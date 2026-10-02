using System.Collections.Generic;
using Margin.Combat;
using Margin.Core;
using Margin.FX;
using Margin.Level;
using UnityEngine;

namespace Margin.Bosses
{
    /// <summary>
    /// Boss 1, The Highlighter (spec 10: tests parrying and reaction). A giant highlighter marker.
    ///
    /// Phase 1 (on the floor):
    ///   Swipe       leans back with a glint on its tip (telegraph), then swings its whole body forward. Parry it.
    ///   Dash Stroke crouches, flashes red, lies flat and shoots across the floor leaving a glowing streak.
    ///               Can't be parried: jump over it or dash through. Bonks into the wall, dizzy (punish).
    ///   Cap Toss    its cap wobbles, then flies in an arc at you. Parry it and it shoots back into the boss.
    /// Phase 2 (at 50%): it rises into the air and floods the floor with glowing ink; stay on the notebook lines.
    ///   Line Sweep  a dashed guide appears at your height, then it streaks across along it. Parry, jump or dash.
    ///   Drip Rain   dashed lanes mark where ink drops will fall. Can't be parried: step out of the lanes.
    ///   Cap Toss    as before, from the air.
    /// Frame data (startup = telegraph, 20+ frames) and damage live in each move's AttackData; speeds in HighlighterData.
    /// </summary>
    public sealed class HighlighterBoss : BossBase
    {
        public const string Swipe = "Swipe", DashStroke = "DashStroke", CapToss = "CapToss", LineSweep = "LineSweep", DripRain = "DripRain";

        [SerializeField] private HighlighterVisual visual;
        [SerializeField] private InkFlood flood;
        [SerializeField] private Material lineMaterial;

        private static HighlighterData fallback;

        // Pose, set by the moves each tick and drawn by UpdateVisual.
        private float tilt, squash = 1f, capShake;
        private bool lying, capOn = true, sparkle;
        private HighlighterEyes eyes = HighlighterEyes.Closed;

        // Move state.
        private Vector2 glideFrom, glideTo;
        private int dir, recoverFrom;
        private float streakFromX, streakToX, streakY;
        private int streakFade;
        private bool dashing;
        private float sweepY;
        private HighlighterCap cap;
        private readonly List<float> lanes = new List<float>();
        private readonly List<Vector2> drops = new List<Vector2>();
        private AttackData dropAttack;
        private int dropsSpawned;
        private float floodFrom;
        private int floodStart, floodFrames;

        private HighlighterData H
        {
            get
            {
                if (data is HighlighterData h) return h;
                if (fallback == null)
                {
                    fallback = ScriptableObject.CreateInstance<HighlighterData>();
                    fallback.hideFlags = HideFlags.DontSave;
                }
                return fallback;
            }
        }

        public void ConfigureLook(HighlighterVisual look, InkFlood inkFlood, Material material)
        {
            visual = look;
            flood = inkFlood;
            lineMaterial = material;
        }

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

        private float Left => Arena.xMin + 1.4f;
        private float Right => Arena.xMax - 1.4f;
        private float CenterX => Arena.center.x;
        private float HoverCenterY => FloorY + H.hoverHeight + BodyHalf;
        private bool Flying => PhaseIndex >= 1 && Mode != BossMode.Dormant && Mode != BossMode.Defeated && Mode != BossMode.Gone;

        /// <summary>Where a returning cap snaps back on.</summary>
        public Vector2 CapHome => new Vector2(Position.x, FeetY + HighlighterVisual.HipHeight + 2.75f);
        public int CapReturnDelay => H.capReturnDelay;
        public float CapReflectSpeed => H.capReflectSpeed;

        // ---------------- intro ----------------

        protected override void OnDormantTick()
        {
            eyes = HighlighterEyes.Closed;
            squash = 1f + 0.02f * Mathf.Sin(ModeFrames * 0.05f);   // asleep, breathing
            tilt = Mathf.MoveTowards(tilt, 0f, 3f);
        }

        protected override void OnIntroStart(bool repeat)
        {
            eyes = HighlighterEyes.Closed;
            Velocity = Vector2.zero;
        }

        protected override void OnIntroTick()
        {
            float t = ModeFrames / (float)Mathf.Max(1, ModeLength);
            eyes = t < 0.2f ? HighlighterEyes.Closed : HighlighterEyes.Angry;
            // One hop at a third of the way in, landing with a squash and a thud.
            int hopFrame = Mathf.Max(1, ModeLength / 3);
            if (ModeFrames == hopFrame) Velocity.y = 13f;
            squash = Body.Collisions.JustLanded ? 0.8f : Mathf.MoveTowards(squash, 1f, 0.02f);
            if (Body.Collisions.JustLanded && ModeFrames > hopFrame) CameraShake.Shake(0.15f);

            // Title card, only on the full-length (first) intro.
            if (Data != null && ModeLength >= 60 && visual != null)
            {
                float a = Mathf.Clamp01((t - 0.3f) * 5f) * Mathf.Clamp01((1f - t) * 6f);
                visual.ShowTitle(Data.bossName, new Vector2(CenterX, Arena.yMax - 3.5f), a);
            }
        }

        // ---------------- rest (the openings) ----------------

        protected override void OnRestStart()
        {
            sparkle = false;
            lying = false;
            capShake = 0f;
            if (visual != null) visual.ShowTitle("", Vector2.zero, 0f);
        }

        protected override void OnRestTick()
        {
            tilt = Mathf.MoveTowards(tilt, 0f, 6f);
            squash = 1f + 0.03f * Mathf.Sin(ModeFrames * 0.2f);
            eyes = HighlighterEyes.Angry;
            if (Target == null) return;

            if (Flying)
            {
                // Hover off to the side of the player, bobbing.
                float side = Position.x >= Target.Body.Position.x ? 1f : -1f;
                float x = Mathf.Clamp(Target.Body.Position.x + side * H.hoverOffset, Left, Right);
                var goal = new Vector2(x, HoverCenterY + 0.3f * Mathf.Sin(ModeFrames * 0.08f));
                Vector2 v = (goal - Position) * 4f;
                Velocity = Vector2.ClampMagnitude(v, H.flySpeed);
            }
            else
            {
                // Keep a middling distance: walk in when far, back off when crowded.
                float d = DistanceToTarget;
                int walk = d > 6.5f ? Facing : d < 2.2f ? -Facing : 0;
                Velocity.x = Mathf.MoveTowards(Velocity.x, walk * Data.walkSpeed, 0.6f);
            }
        }

        // ---------------- moves ----------------

        protected override string TellSound(BossMoveEntry move)
        {
            if (!string.IsNullOrEmpty(move.tellSound)) return move.tellSound;
            switch (move.move)
            {
                case Swipe: return "boss_swipe_tell";
                case DashStroke: return "boss_dash_tell";
                case CapToss: return "boss_cap_tell";
                case LineSweep: return "boss_sweep_tell";
                case DripRain: return "boss_drip_tell";
                default: return "";
            }
        }

        protected override bool CanUse(BossMoveEntry move)
        {
            if (move.move == CapToss) return cap == null;
            if (move.move == LineSweep || move.move == DripRain) return Flying;
            if (move.move == Swipe || move.move == DashStroke) return !Flying;
            return true;
        }

        protected override void OnMoveStart(BossMoveEntry move)
        {
            dir = Facing;
            recoverFrom = 0;
            glideFrom = Position;
            sparkle = false;
            switch (move.move)
            {
                case DashStroke:
                    Velocity.x = 0f;
                    streakFromX = streakToX = Position.x;
                    streakY = FloorY + 0.06f;
                    streakFade = 0;
                    break;
                case LineSweep:
                {
                    // Sweep at the player's height, starting from the side of the arena the boss is on.
                    float playerY = Target != null ? Target.Body.Position.y : FloorY + 3f;
                    float lowest = (flood != null && flood.Active ? flood.FullSurfaceY : FloorY) + 1f;
                    sweepY = Mathf.Max(playerY, lowest);
                    int side = Position.x < CenterX ? -1 : 1;
                    dir = -side;
                    glideTo = new Vector2(side < 0 ? Left : Right, sweepY + 0.85f);
                    break;
                }
                case DripRain:
                {
                    glideTo = new Vector2(CenterX, Mathf.Min(HoverCenterY + 2.5f, Arena.yMax - BodyHalf - 0.5f));
                    lanes.Clear();
                    float px = Target != null ? Target.Body.Position.x : CenterX;
                    lanes.Add(Mathf.Clamp(px, Left, Right));
                    for (int i = 1, tries = 0; i < H.dripCount && tries < 50; tries++)
                    {
                        float x = Random.Range(Left, Right);
                        bool clear = true;
                        foreach (float l in lanes) clear &= Mathf.Abs(l - x) > 2.5f;
                        if (!clear) continue;
                        lanes.Add(x);
                        i++;
                    }
                    dropsSpawned = 0;
                    dropAttack = move.attack;
                    break;
                }
            }
        }

        protected override bool TickMove(BossMoveEntry move, int f)
        {
            AttackData a = move.attack;
            int s = a.startupFrames, act = a.activeFrames, rec = a.recoveryFrames;
            switch (move.move)
            {
                case Swipe:
                    Velocity.x = 0f;
                    if (f <= s)
                    {
                        float t = f / (float)s;
                        tilt = Mathf.Lerp(0f, -40f, 1f - (1f - t) * (1f - t));
                        squash = 1f - 0.12f * t;
                        sparkle = f > s / 3;
                    }
                    else if (f <= s + act)
                    {
                        sparkle = false;
                        tilt = Mathf.Lerp(-40f, 105f, (f - s) / (float)act);
                        squash = 1.05f;
                        Velocity.x = Facing * H.swipeLunge;
                        foreach (HitboxWindow w in a.WindowsAt(f))
                            foreach (HitboxShape shape in w.boxes)
                                Strike(a, HitboxMath.ToWorld(Position.x, Position.y, Facing, shape.offset.x, shape.offset.y,
                                                             shape.size.x, shape.size.y));
                    }
                    else
                    {
                        float t = (f - s - act) / (float)Mathf.Max(1, rec);
                        tilt = t < 0.6f ? 105f : Mathf.Lerp(105f, 0f, (t - 0.6f) / 0.4f);
                        squash = Mathf.MoveTowards(squash, 1f, 0.02f);
                    }
                    return f < a.TotalFrames;

                case DashStroke:
                    Facing = dir;
                    if (f <= s)
                    {
                        Velocity.x = 0f;
                        squash = f > s - 8 ? 1f : 1f - 0.2f * f / s;
                        tilt = f > s - 8 ? Mathf.Lerp(0f, 90f, (f - (s - 8)) / 8f) : 0f;
                        lying = tilt > 45f;
                        eyes = HighlighterEyes.Angry;
                        return true;
                    }
                    if (recoverFrom == 0)
                    {
                        lying = true;
                        tilt = 90f;
                        squash = 1f;
                        Velocity.x = dir * H.dashSpeed;
                        Strike(a, LyingBox());
                        dashing = true;
                        streakToX = Position.x;
                        streakFade = H.dashTrailFrames;
                        bool bonked = dir > 0 ? Body.Collisions.HitWallRight : Body.Collisions.HitWallLeft;
                        if (bonked || f - s >= act)
                        {
                            recoverFrom = f;
                            dashing = false;
                            Velocity.x = 0f;
                            if (bonked) CameraShake.Shake(0.12f);
                        }
                        return true;
                    }
                    else
                    {
                        int k = f - recoverFrom;
                        Velocity.x = 0f;
                        eyes = HighlighterEyes.Dizzy;
                        tilt = k < rec - 14 ? 90f : Mathf.Lerp(90f, 0f, (k - (rec - 14)) / 14f);
                        lying = tilt > 45f;
                        return k < rec;
                    }

                case CapToss:
                    if (!Flying) Velocity.x = 0f;
                    else Velocity = Vector2.MoveTowards(Velocity, Vector2.zero, 1f);
                    if (f <= s)
                    {
                        capShake = 8f * Mathf.Sin(f * 1.4f) * (f / (float)s);
                        squash = 1f - 0.08f * f / s;
                    }
                    else if (f == s + 1)
                    {
                        capShake = 0f;
                        ThrowCap(a);
                    }
                    else squash = Mathf.MoveTowards(squash, 1f, 0.03f);
                    return f < a.TotalFrames;

                case LineSweep:
                    Facing = dir;
                    if (f <= s)
                    {
                        float t = Mathf.Clamp01(f / (float)(s - 6));
                        Glide(glideTo, t);
                        tilt = f > s - 10 ? Mathf.Lerp(0f, 90f, (f - (s - 10)) / 10f) : 0f;
                        lying = tilt > 45f;
                        DrawGuide(Mathf.Clamp01(f / (float)s));
                        return true;
                    }
                    if (recoverFrom == 0)
                    {
                        lying = true;
                        tilt = 90f;
                        Velocity = new Vector2(dir * H.sweepSpeed, 0f);
                        Strike(a, LyingBox());
                        DrawGuide(1f - (f - s) / 12f);
                        bool across = dir > 0 ? Position.x >= Right : Position.x <= Left;
                        if (across || f - s >= act)
                        {
                            recoverFrom = f;
                            Velocity = Vector2.zero;
                        }
                        return true;
                    }
                    else
                    {
                        int k = f - recoverFrom;
                        Velocity = Vector2.zero;
                        eyes = HighlighterEyes.Dizzy;
                        tilt = Mathf.Lerp(90f, 0f, k / (float)Mathf.Max(1, rec));
                        lying = tilt > 45f;
                        return k < rec;
                    }

                case DripRain:
                    if (f <= s)
                    {
                        Glide(glideTo, Mathf.Clamp01(f / (float)(s - 4)));
                        squash = 1f + 0.08f * Mathf.Sin(f * 0.9f);
                        DrawLanes(f / (float)s);
                        return true;
                    }
                    Velocity = Vector2.zero;
                    int since = f - s - 1;
                    if (since >= 0 && since % H.dripInterval == 0 && dropsSpawned < lanes.Count)
                        drops.Add(new Vector2(lanes[dropsSpawned++], Arena.yMax - 0.3f));
                    DrawLanes(dropsSpawned < lanes.Count ? 1f : 0f);
                    return f < a.TotalFrames;
            }
            return false;
        }

        protected override void OnMoveEnd(BossMoveEntry move)
        {
            dashing = false;
            sparkle = false;
            capShake = 0f;
            if (visual != null)
            {
                visual.BeginDashes();
                visual.EndDashes();
            }
            if (Mode == BossMode.Attacking && !Flying) Velocity.x = 0f;
        }

        /// <summary>The flat marker's hitbox (dash, sweep): long and low, centered on the drawn barrel.</summary>
        private AabbBox LyingBox() => new AabbBox(Position.x + Facing * 0.27f, FeetY + 0.65f, 3.2f, 1.25f);

        private void Glide(Vector2 to, float t)
        {
            float e = t * t * (3f - 2f * t);
            Vector2 want = Vector2.Lerp(glideFrom, to, e);
            Velocity = (want - Position) / GameTime.TickDelta;
        }

        private void DrawGuide(float alpha)
        {
            if (visual == null) return;
            visual.BeginDashes();
            visual.Dashed(new Vector2(Arena.xMin, sweepY), new Vector2(Arena.xMax, sweepY), alpha);
            visual.EndDashes();
        }

        private void DrawLanes(float alpha)
        {
            if (visual == null) return;
            visual.BeginDashes();
            float bottom = flood != null && flood.Active ? flood.SurfaceY : FloorY;
            foreach (float x in lanes) visual.Dashed(new Vector2(x, Arena.yMax), new Vector2(x, bottom), alpha, 0.45f, 0.45f);
            visual.EndDashes();
        }

        // ---------------- the cap ----------------

        private void ThrowCap(AttackData attack)
        {
            capOn = false;
            Vector2 from = CapHome;
            Vector2 to = Target != null ? Target.Body.Position : from + new Vector2(Facing * 6f, 0f);
            float time = H.capFlightFrames * GameTime.TickDelta;
            float g = H.capGravity;
            var velocity = new Vector2((to.x - from.x) / time, (to.y - from.y) / time + 0.5f * g * time);
            float restY = (flood != null && flood.Active ? flood.FullSurfaceY : FloorY) + 0.3f;
            cap = HighlighterCap.Spawn(this, attack, Settings, from, velocity, g, restY, lineMaterial);
        }

        /// <summary>The cap floated home: back on the boss's head.</summary>
        public void CapReturned(HighlighterCap which)
        {
            if (which == cap) cap = null;
            capOn = true;
        }

        /// <summary>The player parried the cap back: it smacks into the boss.</summary>
        public void CapReflected(HighlighterCap which)
        {
            if (which == cap) cap = null;
            capOn = true;
            if (!CanBeHit) return;
            if (InkSplatter.Instance != null) InkSplatter.Instance.Burst(Position + Vector2.up, Vector2.up, 18);
            CameraShake.Shake(0.15f);
            Margin.Audio.Sfx.Play("cap_reflect");
            TakeDamage(H.capReflectDamage);
            if (!IsBeaten && Mode != BossMode.PhaseShift) Stagger(Data.parryStaggerFrames);
        }

        private void RecallCap()
        {
            if (cap != null) cap.Recall();
        }

        // ---------------- stagger, phase change, defeat ----------------

        protected override void OnStaggerStart()
        {
            Velocity = Vector2.zero;
            lying = false;
            sparkle = false;
            eyes = HighlighterEyes.Dizzy;
            if (visual != null)
            {
                visual.BeginDashes();
                visual.EndDashes();
            }
        }

        protected override void OnStaggerTick()
        {
            eyes = HighlighterEyes.Dizzy;
            tilt = Mathf.MoveTowards(tilt, -12f + 8f * Mathf.Sin(ModeFrames * 0.25f), 8f);
            squash = Mathf.MoveTowards(squash, 0.94f, 0.02f);
            if (Flying)
            {
                // Sags a little, but never into the ink.
                float lowest = (flood != null ? flood.FullSurfaceY : FloorY) + BodyHalf + 0.8f;
                Velocity = new Vector2(0f, Position.y > lowest ? -0.8f : 0f);
            }
            else Velocity.x = 0f;
        }

        protected override void OnPhaseShiftStart(int phase, bool repeat)
        {
            RecallCap();
            lying = false;
            sparkle = false;
            Velocity = Vector2.zero;
            glideFrom = Position;
            glideTo = new Vector2(CenterX, HoverCenterY);
            floodFrom = flood != null ? flood.Level01 : 0f;
            floodStart = repeat ? 5 : 30;
            floodFrames = Mathf.Max(10, Mathf.Min(H.floodRiseFrames, ModeLength - floodStart - 5));
            CameraShake.Shake(0.2f);
        }

        protected override void OnPhaseShiftTick()
        {
            eyes = HighlighterEyes.Angry;
            tilt = Mathf.MoveTowards(tilt, 0f, 6f);
            squash = 1f + 0.06f * Mathf.Sin(ModeFrames * 0.7f);
            Glide(glideTo, Mathf.Clamp01(ModeFrames / (ModeLength * 0.6f)));
            if (flood != null)
                flood.Level01 = Mathf.Max(floodFrom, Mathf.Clamp01((ModeFrames - floodStart) / (float)floodFrames));
            if (ModeFrames % 20 == 0) CameraShake.Shake(0.06f);
        }

        protected override void OnDefeatStart()
        {
            RecallCap();
            Velocity = Vector2.zero;
            lying = false;
            sparkle = false;
            drops.Clear();
            floodFrom = flood != null ? flood.Level01 : 0f;
            if (visual != null)
            {
                visual.BeginDashes();
                visual.EndDashes();
            }
            CameraShake.Shake(0.3f);
        }

        protected override void OnDefeatTick()
        {
            eyes = HighlighterEyes.Crossed;
            tilt = Mathf.MoveTowards(tilt, 80f, 1.2f);
            squash = 1f + 0.03f * Mathf.Sin(ModeFrames * 1.1f);
            if (flood != null) flood.Level01 = floodFrom * (1f - Mathf.Clamp01(ModeFrames / 90f));
            if (ModeFrames % 25 == 0 && InkSplatter.Instance != null)
                InkSplatter.Instance.Burst(Position + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(0f, 1.5f)), Vector2.up, 8);
        }

        protected override void OnVanish()
        {
            if (InkSplatter.Instance != null) InkSplatter.Instance.Burst(Position, Vector2.up, 40);
            CameraShake.Shake(0.3f);
            if (flood != null) flood.Level01 = 0f;
            if (visual != null) visual.SetVisible(false);
        }

        protected override void OnSkippedToBeaten()
        {
            if (cap != null) Destroy(cap.gameObject);
            cap = null;
            drops.Clear();
            streakFade = 0;
            if (flood != null) flood.Level01 = 0f;
            if (visual != null)
            {
                visual.SetVisible(false);
                visual.ShowTitle("", Vector2.zero, 0f);
                visual.SetDrops(drops);
                visual.SetStreak(Vector2.zero, Vector2.zero, 0f);
            }
        }

        protected override void OnReset()
        {
            if (cap != null) Destroy(cap.gameObject);
            cap = null;
            capOn = true;
            tilt = 0f;
            squash = 1f;
            capShake = 0f;
            lying = false;
            sparkle = false;
            eyes = HighlighterEyes.Closed;
            drops.Clear();
            lanes.Clear();
            streakFade = 0;
            dashing = false;
            if (flood != null) flood.Level01 = 0f;
            if (visual != null)
            {
                visual.SetVisible(true);
                visual.ShowTitle("", Vector2.zero, 0f);
                visual.BeginDashes();
                visual.EndDashes();
                visual.SetDrops(drops);
                visual.SetStreak(Vector2.zero, Vector2.zero, 0f);
            }
        }

        // ---------------- every tick ----------------

        protected override void OnAnyTick()
        {
            if (Mode == BossMode.Gone) return;
            if (Flying) MoveFlying();
            else MoveGrounded();

            // Hurtbox follows the pose: tall standing, long and low lying flat.
            if (lying) SetHurtboxShape(new Vector2(Facing * 0.27f, -BodyHalf + 0.65f), new Vector2(3.2f, 1.25f));
            else SetHurtboxShape(Vector2.zero, Data.bodySize);

            TickDrops();
            if (streakFade > 0 && !dashing) streakFade--;
        }

        private void TickDrops()
        {
            if (drops.Count == 0) return;
            float floor = flood != null && flood.Active ? flood.SurfaceY : FloorY;
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                Vector2 d = drops[i];
                d.y -= H.dripSpeed * GameTime.TickDelta;
                drops[i] = d;
                if (dropAttack != null) Strike(dropAttack, new AabbBox(d.x, d.y, 0.45f, 0.7f));
                if (d.y < floor)
                {
                    if (InkSplatter.Instance != null) InkSplatter.Instance.Burst(new Vector2(d.x, floor), Vector2.up, 5);
                    drops.RemoveAt(i);
                }
            }
        }

        protected override void UpdateVisual()
        {
            if (visual == null) return;
            Color? tint = TelegraphFlash ? (MoveFrame / 3 % 2 == 0 ? new Color(0.85f, 0.1f, 0.1f) : new Color(0.45f, 0.05f, 0.05f)) : (Color?)null;
            visual.Pose(Facing, tilt, squash, lying, capOn, capShake, eyes, sparkle, tint, HurtFlash);
            visual.SetDrops(drops);
            visual.SetStreak(new Vector2(streakFromX, streakY), new Vector2(streakToX, streakY),
                             streakFade / (float)Mathf.Max(1, H.dashTrailFrames));
        }
    }
}
