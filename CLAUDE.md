# CLAUDE.md

Persistent instructions for Claude Code. Sections 0, 2, 3, and 4 are copied verbatim from
`MARGIN_Project_Outline.md`, which remains the full source of truth (milestones, combat, enemies, etc.).

## Project Conventions (added during setup)

- **Namespaces:** `Margin.<Folder>`, e.g. `Margin.Core`, `Margin.Input`. Legacy `UnityEngine.Input` is not used.
  Exception: `Scripts/Debug/` uses `Margin.DebugTools`, because a `Margin.Debug` namespace would shadow
  `UnityEngine.Debug` and break every `Debug.Log` call inside project code.
- **Assemblies:** `Assets/_Project/Scripts/Margin.asmdef` (runtime), `Assets/Tests/EditMode/Margin.Tests.EditMode.asmdef` (tests).
- **Pure C# core:** timing and data logic (e.g. `FrameCounter`, `InputBuffer`) has no `UnityEngine` dependency so it
  can be unit tested fast. MonoBehaviours are thin adapters around it.
- **Frame window semantics:** a window of N frames covers N ticks, counting the tick the event happened on as frame 1.
  Example: a jump pressed on tick 100 with a 6 frame buffer is consumable on ticks 100 to 105 and expired on tick 106.
  Coyote time uses the same rule (first airborne tick is frame 1; frame 6 succeeds, frame 7 fails).
- **Input timing:** Input System callbacks fire in `Update`, gameplay runs in `FixedUpdate`. `InputReader` queues presses
  and flushes them into the `InputBuffer` at the start of each fixed tick so no press is lost between ticks.
- **Attack buttons:** Light = left click (also J, gamepad X). Heavy = right click (also K, gamepad Y). Special = U /
  left bumper. Parry = F / right bumper. Dash = L / Shift / gamepad B or right trigger. Grapple = I / middle click /
  gamepad left trigger (or R3). The triggers were moved off attacks to dash/grapple at the developer's request. Pause = Esc / Start ("Menu" map:
  Pause, Navigate, Submit, Cancel; always enabled, while menus turn the "Gameplay" map off via
  `InputReader.SetGameplayInput`).
  Optional tap/hold mode: `TapHoldButton` (pure) is supported by InputReader if an "Attack" action is added to
  MarginControls (tap = light on release, hold = heavy after `attackHoldFrames`); unbound by default because
  separate buttons respond instantly. A finished attack waits while `AttackHoldPending` so late holds still chain.
- **Tick order:** `GameLoop` (one per scene) advances the `FrameCounter` then ticks every `ITickable` by `TickOrder`:
  `InputReader` (-100), then `PlayerController` (0). Pausing/frame-stepping only needs to control `GameLoop`.
- **Layers:** 6 `Ground` (solid), 7 `OneWayPlatform`, 8 `Player`. `KinematicBodyData` holds the masks.
- **Physics:** `KinematicBody2D` tracks its own `Position` and casts from it (Physics2D autoSyncTransforms is off).
  The Rigidbody2D is Kinematic + Interpolate, moved with `MovePosition`, only for smooth rendering.
- **Player states:** only change `PlayerController.Velocity`; the controller moves the body after the state ticks.
  Shared rules (jump, coyote, dash, walls) live in `PlayerController.Check*` helpers.
- **Input in tests:** `PlayerController` reads `IPlayerInput`; PlayMode tests inject a scripted fake and call `Tick()`.
- **Editor code:** `Assets/_Project/Scripts/Editor/` (own `Margin.Editor` assembly, namespace `Margin.EditorTools`).
  Menu **Margin > Build Movement Gym** regenerates `Scenes/Gym.unity`.
- **Debug tools:** `Scripts/Debug/DebugController` auto-spawns in the Editor/dev builds in any scene with a `GameLoop`.
  F1 collision boxes (hitboxes from every `IHitboxSource` in `HitboxSources`: player orange, enemy magenta), F2 frame data overlay (on by default), F3 pause, F4 step one tick, F5 0.25x slow motion.
  Pause/step are `GameLoop.Paused` / `GameLoop.Step()`, so the frame counter and input buffer freeze with the game.
- **Stick figure (M2):** `Scripts/Rendering/`. `StickFigureRig` builds joints + LineRenderers and runs in edit mode.
  Poses are `FigurePose` (pure C# struct; named to avoid clashing with `UnityEngine.Pose`) stored in `PoseData` assets.
  10 joints: Spine, Neck, Shoulder/Elbow/Hip/Knee x Front/Back. "Front" = near limb (ink), "Back" = far limb (grey).
  **Angle rule:** degrees relative to the parent bone, positive = swing toward facing; knees bend negative, elbows positive.
  Menus: **Margin > Open Pose Studio** (posing scene), **Margin > Create Starter Animations** (writes `Data/Poses`,
  `Data/Animations` clips and `Data/PlayerAnimationSet`; never overwrites without asking).
  Animation: `PoseClip` (entries: pose, frames, easing; loop; fadeInFrames) played by `PoseAnimator` on the rig.
  `PlayerAnimator` (TickOrder 10, after the player) maps states to clips via `PlayerAnimationSet`, so animation
  pauses/frame-steps with the game. **Margin > Wire Player References** upgrades a capsule player to the stick figure.
  Smoothness: `PoseEasing.Smooth` = Catmull-Rom through neighbouring keys (cycles use it); `PoseAnimator` blends
  previous->current tick pose in LateUpdate (like Rigidbody2D interpolation; holds still when it didn't tick) and
  applies `SecondaryMotion` springs per `PoseMotionSettings` (head/back arm sprung; sword arm and legs never, so
  attacks and foot contact stay exact); `PlayerAnimator` leans into ground acceleration (run/idle only).
  Cycles: 8 keys from `Gait` tables in StarterPoses (Run1-8, Sprint1-8, Walk1-8, WalkArmed1-8; weapon arm held
  level via `HoldsWeapon`); **Margin > Upgrade Run Cycles**. Full player set (multi-key clips + transitions) lives in
  `Editor/StarterAnimations.cs` (**Margin > Upgrade Animations**); its poses are generated from offline FK checks
  (feet within ~1 cm of the floor, blade never under it), so edit values there rather than guessing.
  `FigurePose.rootRotation` tilts the whole body around the hips (lying down). Foot lock: `PoseClip.strideLength`
  + pure `CycleSync` set `PoseAnimator.PlaybackRate` from movement speed (player run/sprint, enemy walks).
  One-shot transition clips (turn, runStop, hardLand by `LastFallHeight`, parrySuccess, idleFidget) are visual only:
  any state change except into Idle cancels them. Enemy knockdown: a hard hit in the air (|knockback.y| >=
  `knockdownLaunchSpeed`, heavy hit while airborne, or slam) sets `HardAirHit`; landing enters `EnemyKnockdownState`
  (lie `knockdownFrames`, get up `getUpFrames`, invulnerable by default). Enemy clips (idle loops + breathing,
  turn, alert, hurt, knockdown, get-up, defeat) live in `StarterEnemies.ApplyAnimations` (also run by Upgrade
  Animations); the bat's body motion (bob/bank, tumble, belly-up, flip back over) is in `FlyingEnemy.UpdateVisual`.
- **Sword look + traced poses:** `WeaponLook` (Data/Weapons/KatanaLook, Data/Enemies/PencilLook) styles the blade
  drawn by `WeaponLine`: Katana = gray fill over an ink outline, guard, handle, scabbard at the hip; Pencil for the
  lancer. The tip stays at hand + dir x length, so hitboxes/trail/smears ignore the look. During attacks only
  (`WeaponLine.Attacking`, set by PlayerCombat Begin/EndAttack) the katana's curve flips so the edge leads the swing
  (pure `WeaponShape.EdgeSide`); otherwise it rests edge-down (back of the blade on top, from the blade's own direction). `FigurePose.grip` turns
  the sword away from the forearm (wrist). Two-handed grip is runtime IK (`PlaceBackHand`) only while the blade
  points forward/up. Player poses and clips come from the katana sprite sheets, traced offline into rig angles and
  generated into `Editor/StarterAnimations.TracedData.cs` (`ApplyTraced`, run by **Upgrade Animations**). The data
  is cleaned offline: body tilt folded into spine/hips (exact), loops robust-smoothed, hidden back-arm outliers
  repaired. Run/Sprint are foot-driven (N+-style bounding run with a flight phase): the planted foot slides back at a
  constant speed for a short stance, then the leg trails straight out behind, folds, the knee drives and the foot
  reaches; hip/knee from 2-bone IK, a key every frame, so the measured stride is exact
  (run 3.87 u = ~5 steps/s at 10 u/s; sprint 5.08 u). Upper body (lean, sword hold) from the drawings. Jump/Apex/Fall are a designed N+-style family (`air_family` in the offline tool: push-off, relaxed tuck, open
  at the top, legs reaching down with the free arm up; the fall loop only flutters); only the sword arm comes from
  the drawings (the traced jump flicked a leg, the traced fall pedalled). Idle/IdleFidget keep both feet planted at
  fixed spots (legs solved by IK, knees give as the hips breathe); IdleBreathing's legs/hips are held at its first
  pose so the additive layer only moves torso, head and arms. Air posing
  is N+-style: `AirPoseBlend` (pure) + `PoseAnimator.SetBlend` flow jump -> apex -> fall with vertical speed
  (`PlayerAnimationSet.airBlendRiseSpeed/FallSpeed`; 0 = old threshold switching). Attack clips are 9 keys (Entry, Windup,
  WindupDeep, SwingMid, Strike, StrikeEnd, FollowThrough, Recover, Exit; `AttackSpec`). SwingMid splits the swing so
  a ~180 deg swing can't blend the wrong way round (Light2 and Air Slam go over the head, Rising Moon under) timed from each move's
  startup/active/recovery: the wind-up drifts (no frozen holds), a 2-frame EaseIn swing lands Strike on the first
  active frame, Strike->StrikeEnd spans the active frames (blade crosses the existing hitbox on every one), then the
  follow-through overshoots and settles. Easing: loops `Smooth` (Catmull-Rom), one-shots `Flow` (monotone cubic:
  flows through keys, never overshoots them) - avoid EaseInOut chains, they stop dead at every key.
- **Combat (M3):** `AttackData` holds frame data, hitboxes (authored per attack, right-facing, flipped by facing),
  cancels and presentation. Attack frame 1 = the tick it starts. `AttackTiming` (pure) owns phase/cancel rules:
  on hit, "Cancels into" + jump/dash inside the window; on whiff, jump/dash from the window start and follow-up
  attacks from window start + `whiffChainDelay` (4). Katana combo tree lives in `Editor/StarterCombat.cs`.
  `Hurtbox` is a registry box (no physics); `PlayerCombat` tests hitboxes after the player moves (`PlayerState.PostMove`).
  Hitstop: below `CombatSettings.globalHitstopThreshold` (8) only attacker/target freeze; at/above it `GameLoop.Freeze`.
  Weapons list only *starter* moves (Light1, Heavy, Up, Air...); follow-ups (Light2/3) come from cancel lists.
  Menus: **Margin > Create Starter Combat Data**, **Margin > Add Training Dummy**.
  `HitResolver` (static) applies hits for every damage source (sword, projectiles). Air: `airGravityScale`,
  `hoverOnHit`; targets use `CombatSettings.juggleGravityScale` in hitstun. Ink: pure `InkMeter` owned by
  `PlayerCombat` (gain on hit, spec decay); attacks with `inkCost` can't start without the ink. `InkWaveProjectile`
  spawns on an attack's first active frame when `projectileSpeed > 0`. HUD: see UI (M5).
  Defense: `PlayerHealth` (IHitReceiver; pure `Health`) parries during `ParryState.IsActive` (parryable attacks only),
  otherwise damages and forces `HitstunState`. `IHitReceiver.ReceiveHit` returns false when the
  hit didn't land (parried). Redraw = Down + Special at 100 ink. `SparringAttacker` makes a dummy attack
  (menu **Margin > Add Sparring Dummy**); attackers implement `IParryable` to be staggered.
- **Enemy combos:** hits during player hitstun land (no per-hit i-frames); the 45 i-frames start the tick hitstun
  ends. Damage per combo hit scales via pure `ComboScaling` (-15% per hit, floor 50%, in CombatSettings). Enemies
  chain into `cancelsInto[0]` at the hit-cancel window only if the player is still in hitstun; a miss never chains,
  so only a string's opener needs the 12+ frame telegraph. Starter string: DummyJab > DummyJab2 > DummyKick.
  Combo breaker: Parry during hitstun with 50 ink enters `ComboBreakerState` (invulnerable, pushes and stuns enemies
  via `PlayerCombat.ComboBreakerPush`, 0 damage).
- **Enemies (M4):** `Scripts/Enemies/`. `EnemyBase` (TickOrder 22) is one component driven by an `EnemyData` asset
  (Data/Enemies): states Patrol > Alert > Approach > Attack, plus Hitstun (hits, launches, parry stagger) and Dead
  (`Enemies/States/EnemyStates.cs`). States set `Velocity`; EnemyBase applies gravity (juggle scale in hitstun) and
  moves the body. `EnemyAttackRunner` runs AttackData (hitboxes, own hitstop, chains `cancelsInto[0]` on hit while the
  player is in hitstun). Attack slots: pure `AttackTokenPool`; capacity = engaged enemies, capped by
  `CombatSettings.maxEnemyAttackers` (0 = no cap, the developer's choice: everyone attacks). Player death:
  `DefeatedState` for `playerDeathFrames` (90), then `PlayerHealth.Respawn` at `SpawnPoint` (full health, 0 ink) and
  `PlayerEvents.Respawned`, which resets every enemy. Spacing: `EnemyData.minAttackRange` / `retreatDistance`
  (spear users back off); `weaponLength` adds a `WeaponLine` (the Lancer's pencil). Flyers: `FlyingEnemy : EnemyBase`
  swaps in `FlyPatrolState`, `FlyApproachState`, `DiveAttackState` (Enemies/States/FlyingStates.cs), gravity only
  while stunned/dead, drawn by `Rendering/ScribbleBatVisual` (procedural, no rig). Menus: **Margin > Build Enemy
  Arena** (grunt, lancer, bat), **Add Doodle Grunt**, **Add Pencil Lancer**, **Add Scribble Bat**,
  **Create Starter Enemy Data**. `TrainingDummy`/`SparringAttacker` stay as practice tools (not EnemyBase).
- **Feel (M3 chunk 3):** `Scripts/FX/`. `FeelSettings` asset (Data/FeelSettings) tunes shake, smear, trail,
  splatter, afterimages. `CameraShake` (camera, real-time, uses AttackData.screenShake x screenShakeScale),
  `InkSplatter` (one per scene, listens to `CombatEvents.Hit`), `PlayerFX` (TickOrder 11: smear on swing frames,
  blade trail while attacking, dash afterimages; frozen in hitstop). `FeelBootstrap` adds any missing ones at runtime.
- **UI (M5):** `Scripts/UI/`, UI Toolkit built entirely in code (no UXML/USS). `MarginUI` (one per scene; added by
  the builders, **Margin > Add HUD and Pause Menu**, or automatically in scenes with a GameLoop) hosts a code-made
  UIDocument scaled from 1920x1080, with `HudView` (health + ink card top-left with damage chip, low-health pulse,
  hurt shake, combo counter and breaker prompt; boss bar bottom-centre for any `IBossBarSource` in `BossBars`,
  e.g. an enemy with `EnemyData.bossBarName`) and `PauseMenuView` (flat `UIButton`s with pack icons: Resume,
  Restart = `PlayerHealth.Respawn`, Controls, Quit; Controls page shows `KeyHint` key/mouse pictures per binding,
  text for gamepad). Look: "clean flat + ink": menu cards/HUD bars hand-drawn, buttons flat rounded (UI Toolkit
  borders, not sliced sprites). Art packs live in `Art/UI` (UIElements icons, InputIcons keys; white images tinted in
  code); `StarterUI` fills `UISettings` icon slots and `keyIcons` (only empty slots). Pure `KeyIcons` maps binding
  paths to icon names. SimplePixelUI/SimpleSpinner (Assets root) are unused. Pausing sets `GameLoop.Paused` and `Time.timeScale = 0` and restores
  both on resume. The hand-drawn look is pure `InkLines` (seeded wobble, overshooting corners, pen pressure)
  drawn by `InkPainter` (Painter2D) inside `InkElement`s, re-seeded every `boilFrames` for a subtle boil.
  Pure logic: `InkLines`, `ChipBar`, `MenuCursor`, `BossBars`. Tuning: `Data/UI/UISettings` (+ `MarginTheme.tss`).
  The F2 debug overlay sits top-right because the HUD owns the top-left.
- **Rooms (M5):** `Scripts/Level/`. A world is one scene of `Room`s (camera bounds = confiner + pit line; children
  hold geometry, enemies, doors, pots, decor) placed 200 units apart; `LevelDirector` (TickOrder 30) keeps only the
  current room active. `RoomDoor`s link in pairs; touching one fades to paper (`TransitionTimer`), switches rooms and
  arrives walking in (`IScriptedInput` on InputReader; `PlayerHealth.Protected` meanwhile). Falling below a room:
  `LevelSettings.pitDamage` via `PlayerHealth.TakeHazardDamage`, back to the entry door's safe point. Death fades out
  over the defeat, `Respawn` goes to the `CheckpointRoom`. `Checkpoint` = ink pot (sets SpawnPoint, heals).
  Entering a room calls `IRoomReset.ResetForRoom` on its contents (enemies, bosses). `LevelBlock` draws geometry from
  its BoxCollider2D (ink outline + hatch, or a notebook line for one-way) and redraws in the editor. Decor:
  `ParallaxLayer` (scroll 0.9 ruled lines, 0.6 doodles, 1.1 smudges), shapes from pure `LevelArt`.
  **Camera:** `CameraFollow` + `CameraSettings` (custom, not Cinemachine: no package, counts game frames):
  dead zone 0.6 wide / +2.4 / -1.2, look-ahead 0.5, grounded recentering, room confiner, snaps on `LevelEvents.CameraCut`.
  Menu **Margin > Build World 1** writes `Scenes/World1.unity` (first in Build Settings): 9 rooms from
  `Editor/GymBuilder.World.cs`; edit layouts there (rooms are graybox-simple on purpose until playtesting).
- **Bosses (M5):** `Scripts/Bosses/`. `BossBase` (TickOrder 22): Dormant > Intro > Rest/Attacking (pool from the
  current `BossPhase`, pure `BossMovePicker`: weighted, range-checked, never 3 in a row) > Staggered (parry) /
  PhaseShift (at each phase's healthThreshold) > Defeated > Gone (`Beaten`). Super armor: only parries stagger.
  Intros/phase changes play short after the first view. `BossArena` on the arena Room: fight starts past triggerX,
  seals the entrance, deaths respawn at its retry point (retry under 3 s), beating it opens the exit + shows the
  reward. The Highlighter (`Bosses/Highlighter/`): Swipe (parry), Dash Stroke (red, jump/dash), Cap Toss (parry it
  back = 45 dmg + stagger); phase 2 flies, floods the floor (`Level/InkFlood`, 1-frame hitstun bounce) with Line Sweep
  (parry) and Drip Rain (red, lanes). Data in `Data/Bosses/Highlighter` (`Editor/StarterBosses.cs`, never overwritten).
- **Grapple Line (M5, spec 8):** unlocked by the `AbilityPickup` the Highlighter's arena shows when beaten
  (`AbilityUnlocks.Unlock`, `AbilityEvents.Unlocked`). `PlayerController.CheckGrapple` (air + ground states) hooks the
  best `GrappleAnchor` ring (pure `GrappleAim` score: in `grappleRange`, up-and-ahead preferred, line of sight) or
  yanks an `IGrappleTarget` enemy ahead (`EnemyData.grapplePullable`). `GrappleState`: rope constraint (never farther
  than RopeLength), pump with Left/Right, reel Up/Down, a short yank on attach; Jump lets go with a boost and gives
  the air dash back. `GrappleRope` (auto-added) draws the line from the back hand and highlights the target ring;
  `PoseAnimator.Tilt` leans the body along the line. Tuning in MovementData (Grapple Line header).
- **Audio (M5, spec 13):** `Scripts/Audio/`. Sounds are the developer's pack in `Audio/ink-audio-pack-v1`
  (48 kHz WAV; `name_N` = variant N). `SoundBank` (Data/Audio, filled by **Margin > Set Up Audio** / the builders,
  only empty entries) maps ids to clips + per-sound volume/pitch spread. `AudioDirector` (one per scene, auto-added;
  TickOrder 40) listens to events, never called by gameplay: steps every half stride, whoosh the frame before an
  attack's first active frame (`AttackData.swingSound`), `hitSound` on hits, `CombatEvents.Swing` for enemies,
  `BossEvents` (tell sound per move via `BossMoveEntry.tellSound` or the boss's default), level/ability events;
  UI and one-offs use `Sfx.Play(id)`. Music: world loop; boss + layer stems start together with `PlayScheduled`,
  the layer fades in over 2 bars at phase 2. `MarginAudioImport` streams music, decompresses SFX (first import only).
- **Status:** see `git log` and tags (`m1`, `m2`, ...) for milestone progress. M1 complete (commit 0782ccb).

## 0. How to Use This Document (Instructions for Claude Code)

This is the master spec for a solo Unity project. Treat it as the source of truth.

- Work one milestone at a time, in order. Do not start a milestone until the previous one meets its acceptance criteria.
- The developer is learning C#. Explain non-obvious code in short comments. Prefer readable code over clever code.
- Ask before adding third-party packages, changing folder structure, or refactoring systems from earlier milestones.
- All gameplay logic runs on a fixed 60 Hz tick. Express all timing in frames, not seconds, in data and code.
- All tunable numbers live in ScriptableObjects, never hardcoded in MonoBehaviours.
- Commit after each completed task with a clear message. Use Git with a Unity .gitignore and Git LFS for binary assets.
- The developer cannot easily produce hand-drawn art. All character visuals are procedural (see Section 4). Do not create workflows that depend on sprite sheets.

Recommended: copy Sections 0, 2, 3, and 4 into a CLAUDE.md at the repo root so they persist across sessions.

## 2. Tech Stack

| Area | Choice | Notes |
|---|---|---|
| Engine | Unity 6 LTS (or latest LTS installed) | 2D URP template |
| Language | C# | |
| Input | Unity Input System package | Keyboard and gamepad from day one |
| Rendering | URP 2D Renderer | 2D lights optional, off by default |
| Physics | Custom kinematic controller using Physics2D casts | Do NOT use dynamic Rigidbody2D for the player or enemies |
| Camera | Cinemachine 2D | Confiner per room |
| Tweening | DOTween (free) or custom | Ask before adding |
| Version control | Git + Git LFS | Unity .gitignore |
| Platform | Windows PC first | Steam Deck check later |

**Project settings:**

- `Time.fixedDeltaTime = 1/60`
- VSync on, target frame rate uncapped, rendering interpolates between fixed ticks
- Pixels per unit: 100. Player height: about 1.8 units.

## 3. Architecture

### 3.1 Folder Structure

```
Assets/
  _Project/
    Scripts/
      Core/          (GameLoop, FrameTimer, TimeScale/Hitstop, Services)
      Input/         (InputReader, InputBuffer)
      Player/        (PlayerController, PlayerStateMachine, states)
      Physics/       (KinematicBody2D, CollisionSolver)
      Combat/        (Hitbox, Hurtbox, AttackData, DamageInfo, HitResolver)
      Weapons/       (WeaponData, WeaponController)
      Abilities/     (AbilityData, AbilityUnlocks, ability implementations)
      Enemies/       (EnemyBase, EnemyStateMachine, AI behaviors)
      Bosses/        (BossBase, BossPhase, per-boss folders)
      Rendering/     (StickFigureRig, PoseData, PoseAnimator, SmearRenderer)
      FX/            (HitFX, InkSplatter, ScreenShake, Afterimage)
      Level/         (Room, RoomTransition, Checkpoint, Hazards)
      UI/            (HUD, InkMeter, Menus)
      Save/          (SaveData, SaveSystem)
      Debug/         (FrameDataOverlay, HitboxVisualizer, DebugConsole)
    Data/            (ScriptableObject assets: Movement, Attacks, Weapons, Poses)
    Prefabs/
    Scenes/
    Art/             (paper textures, ink brushes, UI)
    Audio/
  Tests/
    EditMode/
    PlayMode/
```

### 3.2 Core Patterns

- **Finite state machines** for player, enemies, and bosses. Each state is a class with `Enter`, `Tick` (fixed), `Exit`, and `CanTransition` checks.
- **Data-driven attacks.** An `AttackData` ScriptableObject defines everything about a move (Section 6.2). Code reads data; designers tweak data.
- **Event bus** (simple static C# events or a ScriptableObject event channel) for decoupling: `OnHit`, `OnPlayerDamaged`, `OnBossPhaseChange`, `OnAbilityUnlocked`.
- **Frame counter** as the single time source for gameplay. Everything that counts time counts frames.

### 3.3 Debug Tools (build early, in Milestone 1)

- Hitbox and hurtbox visualizer (toggle F1)
- Frame data overlay: current state, frame count in state, velocity, grounded flag, buffered inputs (toggle F2)
- Frame step: pause and advance one tick at a time (F3 pause, F4 step)
- Slow motion toggle: 0.25x (F5)

These tools make tuning game feel possible. Do not skip them.

## 4. Procedural Stick Figure Rendering

Because the developer cannot draw sprite sheets, characters are built from code.

### 4.1 Rig

- A `StickFigureRig` component holds a hierarchy of joints: hips, spine, neck, head, and left/right shoulder, elbow, hand, hip, knee, foot.
- Limbs are drawn with `LineRenderer` (or a single custom mesh for performance) using a round cap and slight width variation to look hand-inked.
- Head is a circle drawn with a line loop or a simple sprite.
- Line color: near-black ink (#1A1A1A). Slight wobble shader optional (Milestone 8).

### 4.2 Poses and Animation

- A `PoseData` ScriptableObject stores joint rotations for one keyframe.
- A `PoseClip` ScriptableObject is a list of (PoseData, frameDuration, easing) entries.
- `PoseAnimator` interpolates between poses each tick. Supports:
  - Hard snap (no interpolation), used for attack active frames to feel crisp
  - Ease in/out for idle and anticipation
  - Additive layers (e.g., breathing on top of idle)
- Include an editor tool (custom inspector or EditorWindow) to pose the rig in the Scene view by rotating joints and save the result as a `PoseData`. This is how the developer will author animations.

### 4.3 Weapon Visuals (animation budget goes here)

- **Smear frames:** On the fastest frame of a swing, render a procedural arc mesh (a filled crescent from the weapon's previous angle to current angle). Lasts 1 to 3 frames.
- **Motion trail:** `TrailRenderer` on the weapon tip, ink-colored, short lifetime (0.08 to 0.15 s).
- **Anticipation:** Every heavy attack has 2 to 4 frames of wind-up pose before the swing.
- **Afterimages:** On dash, spawn 3 to 4 fading copies of the current line pose.
- **Ink splatter:** Particle burst at hit point, direction based on attack angle.
