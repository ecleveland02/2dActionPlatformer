using System.Collections.Generic;
using Margin.Abilities;
using Margin.Bosses;
using Margin.Combat;
using Margin.Core;
using Margin.Enemies;
using Margin.Level;
using Margin.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Margin.Audio
{
    /// <summary>
    /// Plays the game's sounds and music (spec 13). One per scene, added by the level builders or automatically in
    /// any scene with a GameLoop. It listens to the game's events rather than being called by gameplay code:
    ///
    ///   player    steps (every half stride of running), jump, land, dash, grapple, swing whooshes (the frame
    ///             before the first active frame), hurt, death
    ///   combat    hits (AttackData.hitSound, pitch +-5%), parries, enemy swings and defeats
    ///   bosses    each move's telegraph sound, phase change, defeat; the boss track starts with the fight and
    ///             its layer fades in over two bars at phase 2 (both stems start on the same audio clock)
    ///   level     page turn between rooms, ink pot chime, ability fanfare; UI clicks come through Sfx.Play
    /// Gameplay cues are checked on the game tick (TickOrder 40), so they pause and frame-step with the game.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class AudioDirector : MonoBehaviour, ITickable
    {
        private const string BankPath = "Assets/_Project/Data/Audio/SoundBank.asset";
        private const int Voices = 16;

        [SerializeField] private SoundBank bank;

        private AudioSource[] voices;
        private int nextVoice;
        private AudioSource world, boss, layer;
        private readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>();

        private PlayerController player;
        private float nextSearch;
        private float stepDistance;
        private Vector2 lastPosition;
        private int whooshedSerial = -1, lastPullFrames;
        private bool roomSeen;

        // Music targets, faded toward in real time.
        private bool bossMusicOn;
        private float worldLevel, bossLevel, layerLevel, layerTarget;

        public int TickOrder => 40;
        public SoundBank Bank => bank;

        private float SfxGain => bank != null ? bank.masterVolume * bank.sfxVolume : 0f;
        private float MusicGain => bank != null ? bank.masterVolume * bank.musicVolume : 0f;

        private void Awake()
        {
            if (bank == null) bank = FindBank();
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++) voices[i] = NewSource("Voice " + i, false);
            world = NewSource("World Music", true);
            boss = NewSource("Boss Music", true);
            layer = NewSource("Boss Layer", true);
        }

        private AudioSource NewSource(string sourceName, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;   // 2D: the camera follows the player, so everything plays centered
            return source;
        }

        private void Start()
        {
            if (bank != null && bank.worldMusic != null)
            {
                world.clip = bank.worldMusic;
                world.volume = 0f;
                world.Play();
            }
        }

        private void OnEnable()
        {
            GameLoop.Register(this);
            Sfx.Requested += Play;
            CombatEvents.Hit += OnHit;
            CombatEvents.Parry += OnParry;
            CombatEvents.Swing += OnEnemySwing;
            EnemyBase.Vanished += OnEnemyVanished;
            PlayerEvents.Died += OnPlayerDied;
            LevelEvents.RoomEntered += OnRoomEntered;
            LevelEvents.CheckpointReached += OnCheckpoint;
            AbilityEvents.Unlocked += OnUnlocked;
            BossEvents.Engaged += OnBossEngaged;
            BossEvents.MoveStarted += OnBossMove;
            BossEvents.PhaseChanged += OnBossPhase;
            BossEvents.Beaten += OnBossBeaten;
            BossEvents.Reset += OnBossReset;
        }

        private void OnDisable()
        {
            GameLoop.Unregister(this);
            Sfx.Requested -= Play;
            CombatEvents.Hit -= OnHit;
            CombatEvents.Parry -= OnParry;
            CombatEvents.Swing -= OnEnemySwing;
            EnemyBase.Vanished -= OnEnemyVanished;
            PlayerEvents.Died -= OnPlayerDied;
            LevelEvents.RoomEntered -= OnRoomEntered;
            LevelEvents.CheckpointReached -= OnCheckpoint;
            AbilityEvents.Unlocked -= OnUnlocked;
            BossEvents.Engaged -= OnBossEngaged;
            BossEvents.MoveStarted -= OnBossMove;
            BossEvents.PhaseChanged -= OnBossPhase;
            BossEvents.Beaten -= OnBossBeaten;
            BossEvents.Reset -= OnBossReset;
            if (player != null) player.StateChanged -= OnPlayerState;
            player = null;
        }

        // ---------------- playing ----------------

        /// <summary>Plays a sound from the bank by id (random variant, the sound's volume and pitch spread).</summary>
        public void Play(string id, float volume = 1f)
        {
            SoundBank.Sound sound = bank != null ? bank.Find(id) : null;
            if (sound == null || sound.variants == null || sound.variants.Count == 0) return;

            lastVariant.TryGetValue(id, out int last);
            int pick = AudioMath.PickVariant(sound.variants.Count, lastVariant.ContainsKey(id) ? last : -1, Random.value);
            lastVariant[id] = pick;
            AudioClip clip = sound.variants[pick];
            if (clip == null) return;

            AudioSource source = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            source.clip = clip;
            source.volume = Mathf.Clamp01(sound.volume * volume * SfxGain);
            source.pitch = 1f + Random.Range(-sound.pitchVariance, sound.pitchVariance);
            source.Play();
        }

        // ---------------- gameplay cues (on the game tick) ----------------

        public void Tick()
        {
            if (player == null || bank == null || player.Body == null) return;

            // Footsteps: a pen scratch every half stride while running on the ground.
            Vector2 p = player.Body.Position;
            float moved = Mathf.Abs(p.x - lastPosition.x);
            lastPosition = p;
            if (player.CurrentState is RunState && player.Grounded)
            {
                stepDistance += moved;
                float stride = player.IsSprinting ? bank.sprintStepDistance : bank.runStepDistance;
                if (stepDistance >= stride)
                {
                    stepDistance -= stride;
                    Play("step");
                }
            }

            // Sword whoosh on the frame before the swing lands, once per attack.
            if (player.CurrentState is AttackState attack && attack.Attack != null && player.Combat != null &&
                player.Combat.AttackSerial != whooshedSerial &&
                attack.Frame >= Mathf.Max(1, attack.Attack.Timing.FirstActiveFrame - 1))
            {
                whooshedSerial = player.Combat.AttackSerial;
                Play(attack.Attack.swingSound);
            }

            // The Grapple Line yanked an enemy this tick.
            if (player.PullLineFrames > lastPullFrames)
            {
                Play("grapple_throw");
                Play("grapple_hook");
            }
            lastPullFrames = player.PullLineFrames;
        }

        private void OnPlayerState(PlayerState previous, PlayerState next)
        {
            switch (next)
            {
                case JumpState _:   // includes WallJumpState
                    Play("jump");
                    break;
                case LandState _:
                    Play("land", Mathf.Clamp(0.5f + player.LastFallHeight * 0.1f, 0.5f, 1f));
                    break;
                case DashState _:
                    Play("dash");
                    break;
                case GrappleState _:
                    Play("grapple_throw");
                    Play("grapple_hook");
                    break;
                case RunState _:
                    if (!(previous is RunState)) stepDistance = bank != null ? bank.runStepDistance * 0.5f : 0f;
                    break;
            }
        }

        private void OnHit(HitInfo hit, IHitReceiver target)
        {
            if (target is PlayerHealth) Play("player_hurt");
            else Play(string.IsNullOrEmpty(hit.Attack.hitSound) ? "hit_light" : hit.Attack.hitSound);
        }

        private void OnParry(HitInfo hit) => Play("parry");
        private void OnEnemySwing(Component attacker, AttackData attack) => Play(attack.swingSound, 0.7f);
        private void OnEnemyVanished(EnemyBase enemy) => Play("enemy_defeat");
        private void OnPlayerDied(PlayerController who) => Play("player_death");
        private void OnCheckpoint(Checkpoint checkpoint) => Play("checkpoint");
        private void OnUnlocked(Ability ability) => Play("ability_get");

        private void OnRoomEntered(Room room)
        {
            // The first room is where the scene starts: no page turn.
            if (roomSeen) Play("page_turn");
            roomSeen = true;
        }

        // ---------------- bosses and music ----------------

        private void OnBossMove(BossBase who, string tell)
        {
            if (!string.IsNullOrEmpty(tell)) Play(tell);
        }

        private void OnBossEngaged(BossBase who)
        {
            if (bank == null || bank.bossMusic == null) return;
            // Both stems start on the same audio clock tick so the layer stays in time when it fades in.
            double start = AudioSettings.dspTime + 0.2;
            boss.clip = bank.bossMusic;
            boss.volume = 0f;
            boss.PlayScheduled(start);
            if (bank.bossLayer != null)
            {
                layer.clip = bank.bossLayer;
                layer.volume = 0f;
                layer.PlayScheduled(start);
            }
            bossMusicOn = true;
            layerTarget = 0f;
            layerLevel = 0f;
        }

        private void OnBossPhase(BossBase who, int phase)
        {
            if (phase >= 1) layerTarget = 1f;
            Play("boss_phase");
        }

        private void OnBossBeaten(BossBase who)
        {
            Play("boss_defeat");
            StopBossMusic();
        }

        private void OnBossReset(BossBase who)
        {
            if (bossMusicOn) StopBossMusic();
        }

        private void StopBossMusic()
        {
            bossMusicOn = false;
            layerTarget = 0f;
        }

        /// <summary>Music fades in real time, so they keep moving while paused or in hitstop.</summary>
        private void Update()
        {
            if (player == null && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 0.5f;
                player = SceneQuery.FindFirst<PlayerController>();
                if (player != null)
                {
                    player.StateChanged += OnPlayerState;
                    lastPosition = player.Body != null ? player.Body.Position : Vector2.zero;
                }
            }

            if (bank == null) return;
            float dt = Time.unscaledDeltaTime;
            float cross = bank.crossfadeSeconds > 0f ? dt / bank.crossfadeSeconds : 1f;
            worldLevel = Mathf.MoveTowards(worldLevel, bossMusicOn ? 0f : 1f, cross);
            bossLevel = Mathf.MoveTowards(bossLevel, bossMusicOn ? 1f : 0f, cross);
            float fadeSeconds = AudioMath.BarsToSeconds(bank.layerFadeBars, bank.bossBpm);
            layerLevel = Mathf.MoveTowards(layerLevel, bossMusicOn ? layerTarget : 0f, fadeSeconds > 0f ? dt / fadeSeconds : 1f);

            float music = MusicGain;
            world.volume = worldLevel * music;
            boss.volume = bossLevel * music;
            layer.volume = layerLevel * bossLevel * music;
            if (!bossMusicOn && bossLevel <= 0f && boss.isPlaying)
            {
                boss.Stop();
                layer.Stop();
            }
        }

        // ---------------- setup ----------------

        private static SoundBank FindBank()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
#else
            return null;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureAudio();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureAudio();

        /// <summary>Gameplay scenes (with a GameLoop) get an AudioDirector if they don't have one.</summary>
        public static void EnsureAudio()
        {
            if (GameLoop.Clock == null) return;
            if (SceneQuery.FindFirst<AudioDirector>() == null) new GameObject("[Audio]").AddComponent<AudioDirector>();
        }
    }
}
