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
- **Attack buttons:** Light = left click / right trigger (also J, gamepad X). Heavy = right click / left trigger
  (also K, gamepad Y). Special = U / left bumper. Parry = F / right bumper.
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
  F1 collision boxes, F2 frame data overlay (on by default), F3 pause, F4 step one tick, F5 0.25x slow motion.
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
  spawns on an attack's first active frame when `projectileSpeed > 0`. Placeholder HUD: `UI/PlayerHUD` (health + ink).
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
- **Feel (M3 chunk 3):** `Scripts/FX/`. `FeelSettings` asset (Data/FeelSettings) tunes shake, smear, trail,
  splatter, afterimages. `CameraShake` (camera, real-time, uses AttackData.screenShake x screenShakeScale),
  `InkSplatter` (one per scene, listens to `CombatEvents.Hit`), `PlayerFX` (TickOrder 11: smear on swing frames,
  blade trail while attacking, dash afterimages; frozen in hitstop). `FeelBootstrap` adds any missing ones at runtime.
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
