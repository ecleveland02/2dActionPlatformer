using System.Collections.Generic;
using Margin.Bosses;
using Margin.Combat;
using UnityEditor;
using UnityEngine;

namespace Margin.EditorTools
{
    /// <summary>
    /// Creates the bosses' data. The Highlighter's in Data/Bosses/Highlighter (spec 10): one AttackData per move (frame data,
    /// damage, parryability; startup is the telegraph), the flood's hit, two BossPhase assets and HighlighterData.
    /// Only creates what's missing, so tuning done in the Inspector is never overwritten. Run by Build World 1.
    /// </summary>
    public static class StarterBosses
    {
        private const string Folder = "Assets/_Project/Data/Bosses/Highlighter";
        private const string StaplerFolder = "Assets/_Project/Data/Bosses/Stapler";

        internal sealed class Result
        {
            public HighlighterData Highlighter;
            public AttackData Flood;
        }

        [MenuItem("Margin/Create Highlighter Boss Data")]
        public static void CreateFromMenu()
        {
            Result r = EnsureCreated();
            EditorGUIUtility.PingObject(r.Highlighter);
        }

        internal sealed class StaplerResult
        {
            public StaplerData Stapler;
            public AttackData Shockwave;
        }

        [MenuItem("Margin/Create Stapler Titan Boss Data")]
        public static void CreateStaplerFromMenu()
        {
            StaplerResult r = EnsureStapler();
            EditorGUIUtility.PingObject(r.Stapler);
        }

        /// <summary>
        /// The Stapler Titan's data in Data/Bosses/Stapler (spec 10, Boss 2: positioning and punishing openings).
        /// Only creates what's missing. Run by Build World 2.
        /// </summary>
        internal static StaplerResult EnsureStapler()
        {
            StickFigureRigEditor.EnsureFolder(StaplerFolder);
            const string f = StaplerFolder;

            // Chomp: the jaw creaks open with a glint (24 frames), snaps shut as it lunges. Parryable.
            AttackData chomp = Attack("STChomp", 24, 6, 32, damage: 16, hitstop: 7, hitstun: 20, new Vector2(9f, 5f), parryable: true,
                                      shake: 0.12f, box: new HitboxShape { offset = new Vector2(1.9f, 0f), size = new Vector2(2.6f, 1.9f) }, folder: f);
            // Hop Slam: crouch + red flash (28 frames), a 40-frame leap at you, then a 52-frame jam (the punish).
            AttackData hop = Attack("STHopSlam", 28, 40, 52, damage: 18, hitstop: 8, hitstun: 24, new Vector2(8f, 9f), parryable: false,
                                    shake: 0.2f, folder: f);
            // The landing's shockwaves: low enough to jump (or double jump) over.
            AttackData wave = Attack("STShockwave", 1, 1, 0, damage: 12, hitstop: 5, hitstun: 18, new Vector2(5f, 10f), parryable: false,
                                     shake: 0.08f, folder: f);
            // Staple Shot: red flash (22 frames), then 3 aimed staples 12 frames apart.
            AttackData shot = Attack("STStapleShot", 22, 26, 30, damage: 10, hitstop: 4, hitstun: 16, new Vector2(5f, 4f), parryable: false,
                                     shake: 0.06f, folder: f);
            // Staple Rain: jaw to the sky + lanes (30 frames), five staples fall 8 frames apart.
            AttackData rain = Attack("STStapleRain", 30, 40, 34, damage: 12, hitstop: 4, hitstun: 16, new Vector2(3f, 5f), parryable: false,
                                     shake: 0.06f, folder: f);

            BossPhase one = Phase("StaplerPhase1", 1f, 30, 48, 0, 0, f,
                Move(StaplerTitanBoss.Chomp, chomp, 4, 0f, 5f),
                Move(StaplerTitanBoss.HopSlam, hop, 3, 3f, 0f),
                Move(StaplerTitanBoss.StapleShot, shot, 3, 5f, 0f));
            BossPhase two = Phase("StaplerPhase2", 0.5f, 26, 42, 160, 60, f,
                Move(StaplerTitanBoss.Chomp, chomp, 3, 0f, 5f),
                Move(StaplerTitanBoss.HopSlam, hop, 3, 2f, 0f),
                Move(StaplerTitanBoss.StapleRain, rain, 3, 0f, 0f),
                Move(StaplerTitanBoss.StapleShot, shot, 2, 5f, 0f));

            var data = StarterCombat.LoadOrCreate<StaplerData>($"{f}/StaplerTitan.asset", out bool isNew);
            if (isNew)
            {
                data.bossName = "THE STAPLER TITAN";
                data.maxHealth = 560;
                data.introFrames = 120;
                data.introFramesRepeat = 30;
                data.parryStaggerFrames = 70;
                data.defeatFrames = 150;
                data.bodySize = new Vector2(3.2f, 1.7f);
                data.walkSpeed = 3f;
                data.phases = new List<BossPhase> { one, two };
                EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
            return new StaplerResult { Stapler = data, Shockwave = wave };
        }

        internal static Result EnsureCreated()
        {
            StickFigureRigEditor.EnsureFolder(Folder);

            // Swipe: leans back with a glint (22 frames), swings the whole marker forward. Parryable.
            AttackData swipe = Attack("HLSwipe", 22, 6, 34, damage: 14, hitstop: 7, hitstun: 20, new Vector2(9f, 5f), parryable: true,
                                      shake: 0.12f, box: new HitboxShape { offset = new Vector2(1.8f, 0.3f), size = new Vector2(3f, 3.4f) });
            // Dash Stroke: crouch + red flash (30 frames), then flat across the floor until the wall. Jump or dash through.
            AttackData dash = Attack("HLDashStroke", 30, 70, 44, damage: 16, hitstop: 6, hitstun: 22, new Vector2(10f, 8f), parryable: false,
                                     shake: 0.15f);
            // Cap Toss: the cap wobbles (20 frames), then flies in an arc. Parry it back into the boss.
            AttackData cap = Attack("HLCapToss", 20, 1, 30, damage: 10, hitstop: 5, hitstun: 16, new Vector2(6f, 5f), parryable: true,
                                    shake: 0.08f);
            // Line Sweep: a dashed guide at your height (34 frames), then it streaks across. Parryable.
            AttackData sweep = Attack("HLLineSweep", 34, 60, 34, damage: 14, hitstop: 7, hitstun: 20, new Vector2(9f, 6f), parryable: true,
                                      shake: 0.12f);
            // Drip Rain: lanes appear (28 frames), four drops fall one after another. Step aside.
            AttackData drip = Attack("HLDripRain", 28, 40, 36, damage: 10, hitstop: 4, hitstun: 14, new Vector2(3f, 4f), parryable: false,
                                     shake: 0.06f);
            // The flood: hurts and bounces you up with only 1 frame of hitstun, so you can steer back onto a line.
            AttackData flood = Attack("HLInkFlood", 1, 1, 0, damage: 10, hitstop: 3, hitstun: 1, new Vector2(0f, 21f), parryable: false,
                                      shake: 0.05f);

            BossPhase one = Phase("HighlighterPhase1", 1f, 26, 44, 0, 0,
                Move(HighlighterBoss.Swipe, swipe, 4, 0f, 5.5f),
                Move(HighlighterBoss.DashStroke, dash, 3, 3f, 0f),
                Move(HighlighterBoss.CapToss, cap, 3, 4.5f, 0f));
            BossPhase two = Phase("HighlighterPhase2", 0.5f, 34, 52, 150, 75,
                Move(HighlighterBoss.LineSweep, sweep, 4, 0f, 0f),
                Move(HighlighterBoss.DripRain, drip, 3, 0f, 0f),
                Move(HighlighterBoss.CapToss, cap, 3, 0f, 0f));

            var data = StarterCombat.LoadOrCreate<HighlighterData>($"{Folder}/Highlighter.asset", out bool isNew);
            if (isNew)
            {
                data.bossName = "THE HIGHLIGHTER";
                data.maxHealth = 520;
                data.introFrames = 120;
                data.introFramesRepeat = 30;
                data.parryStaggerFrames = 70;
                data.defeatFrames = 150;
                data.bodySize = new Vector2(1.3f, 3f);
                data.walkSpeed = 3.5f;
                data.phases = new List<BossPhase> { one, two };
                EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
            return new Result { Highlighter = data, Flood = flood };
        }

        private static AttackData Attack(string name, int startup, int active, int recovery, int damage, int hitstop, int hitstun,
                                         Vector2 knockback, bool parryable, float shake, HitboxShape? box = null, string folder = Folder)
        {
            var a = StarterCombat.LoadOrCreate<AttackData>($"{folder}/{name}.asset", out bool isNew);
            if (!isNew) return a;
            a.startupFrames = startup;
            a.activeFrames = active;
            a.recoveryFrames = recovery;
            a.damage = damage;
            a.hitstopFrames = hitstop;
            a.hitstunFrames = hitstun;
            a.knockback = knockback;
            a.parryable = parryable;
            a.inkGain = 0;
            a.screenShake = shake;
            a.smear = false;
            a.cancelsIntoJump = false;
            a.cancelsIntoDash = false;
            a.chainsOnWhiff = false;
            a.cancelWindowStart = startup + active + recovery + 1;
            a.cancelWindowEnd = a.cancelWindowStart;
            a.hitboxes = new List<HitboxWindow>();
            if (box.HasValue) a.hitboxes.Add(new HitboxWindow { boxes = new List<HitboxShape> { box.Value } });
            EditorUtility.SetDirty(a);
            return a;
        }

        private static BossMoveEntry Move(string move, AttackData attack, int weight, float minRange, float maxRange) =>
            new BossMoveEntry { move = move, attack = attack, weight = weight, minRange = minRange, maxRange = maxRange };

        private static BossPhase Phase(string name, float threshold, int restMin, int restMax, int transition, int transitionRepeat,
                                       params BossMoveEntry[] moves) =>
            Phase(name, threshold, restMin, restMax, transition, transitionRepeat, Folder, moves);

        private static BossPhase Phase(string name, float threshold, int restMin, int restMax, int transition, int transitionRepeat,
                                       string folder, params BossMoveEntry[] moves)
        {
            var p = StarterCombat.LoadOrCreate<BossPhase>($"{folder}/{name}.asset", out bool isNew);
            if (!isNew) return p;
            p.healthThreshold = threshold;
            p.restFramesMin = restMin;
            p.restFramesMax = restMax;
            p.transitionFrames = transition;
            p.transitionFramesRepeat = transitionRepeat;
            p.moves = new List<BossMoveEntry>(moves);
            EditorUtility.SetDirty(p);
            return p;
        }
    }
}
