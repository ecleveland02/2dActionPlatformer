# MARGIN: Project Outline for Claude Code

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

## 1. Game Summary

**Genre:** 2D single-player action platformer with fighting-game combat feel. Optional local 1v1 arena mode post-launch.

**Premise:** The player is a stick figure doodled in the margin of a student's notebook. Other doodles are being erased. The player fights across notebook pages to find the cause.

**Pillars** (every feature must serve at least one):

1. **Smooth, responsive movement.** Input to action in 1 frame. No floaty jumps. No locked-in animations.
2. **Combat with impact.** Hitstop, screen shake, ink splatter, cancel windows.
3. **Progression through abilities.** Each boss unlocks a tool used in both combat and traversal.
4. **Readable minimalism.** Ink lines on paper. Clarity over detail.

**Scope target (full game):** 6 worlds, 5 bosses plus final boss, 5 weapons, 6 abilities.
**Scope target (vertical slice):** 1 world section, 1 weapon, 1 boss, full movement kit. This is the real first goal.

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

- A `StickFigureRig` component holds a hierarchy of joints: hips, spine, neck, head, and left/right shoulder, elbow, hand, hip, knee, foot. (Implemented as front/back, i.e. near/far limbs, since the view is side-on; back limbs draw lighter.)
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

## 5. Movement System (Milestone 1 focus)

### 5.1 Kinematic Controller

- Custom `KinematicBody2D` using `BoxCast` / `CapsuleCast` against a Ground layer.
- Resolve X and Y separately. Handle slopes up to 45 degrees. One-way platforms supported (drop through with Down + Jump).
- Skin width: 0.015 units.

### 5.2 Movement Tuning (defaults in MovementData ScriptableObject)

| Parameter | Default | Notes |
|---|---|---|
| Run speed | 9 units/s | |
| Ground acceleration | reach max in 4 frames | Snappy start |
| Ground deceleration | stop in 3 frames | No sliding |
| Air acceleration | reach max in 8 frames | Some air control, not full |
| Sprint speed | 13 units/s | Automatic: after 40 frames of continuous running at full speed; ramps up over 15 frames. No sprint button, no walk |
| Sprint skid | 20 frames from full sprint to stop | Turning around above run speed skids, then runs the other way. Jump and dash cancel it |
| Sprint in the air | momentum kept while holding forward | Air control cannot accelerate past run speed. Sprint jump ~8.5 units vs run jump ~6 |
| Jump height (full) | 3.2 units | |
| Jump height (min, early release) | 1.2 units | Variable jump |
| Rise gravity | derived from jump height and 22 frames to apex | |
| Fall gravity multiplier | 1.8x rise gravity | Snappy descent |
| Max fall speed | 20 units/s | |
| Fast fall speed | 28 units/s | Press Down in air after apex |
| Coyote time | 6 frames | |
| Jump input buffer | 6 frames | |
| Apex hang | gravity x0.5 when abs(vy) < 1.5 | Adds control at top of jump |
| Corner correction | nudge up to 0.15 units | Avoid bonking on ledge corners |

### 5.3 Input Buffer

- `InputBuffer` stores each action press with its frame stamp.
- A state checks `buffer.Consume(Action.Jump, withinFrames: 6)`.
- Buffer attack, jump, dash, and parry. Default window 6 frames, configurable per action.

### 5.4 Player States

Idle, Run (includes sprint), Skid, Jump, Fall, Land (2 frames), FastFall, Dash, WallSlide, WallJump, Attack, AirAttack, Parry, Hitstun, Knockdown, Dead

Rule: any state can be interrupted by Hitstun. Attack states expose cancel windows (Section 6.3).

## 6. Combat System

### 6.1 Hitboxes and Hurtboxes

- Hitboxes are simple shapes (box, circle, capsule) attached to rig joints or weapon tip, enabled only on active frames.
- Hurtboxes cover the body. Separate layer masks for player and enemy.
- Each attack instance tracks which targets it has hit so one swing hits each target once (unless multi-hit is set).

### 6.2 AttackData (ScriptableObject)

| Field | Example (Brush Katana light 1) |
|---|---|
| Startup frames | 4 |
| Active frames | 3 |
| Recovery frames | 10 |
| Damage | 8 |
| Hitstop (frames) | 4 |
| Hitstun on target (frames) | 18 |
| Knockback vector | (4, 1) |
| Launches | false |
| Cancel window start/end (on hit) | frame 9 to 17 |
| Cancels into | Light2, Heavy, Dash, Jump |
| Ink gain | 5 |
| Pose clip | KatanaLight1 |
| Smear enabled | true |
| Screen shake | 0.05 |
| Sound | swing_light, hit_light |

### 6.3 Cancels and Combos

- On hit, during the cancel window, the player can cancel into any move listed in Cancels into.
- On whiff, Dash and Jump can cancel from the window start. Follow-up attacks can also chain on whiff, but only 4 frames after the window start (per-attack `whiffChainDelay`), so strings flow while hits are still rewarded. (Changed from "only Dash and Jump on whiff" by the developer.)
- No hardcoded combo strings. Combos emerge from cancel rules.

### 6.4 Hitstop and Feel

- On hit: freeze attacker and target for hitstop frames. World keeps running (other enemies, particles) unless it is a heavy hit (hitstop >= 8), in which case global time scale drops to 0 briefly.
- Target shakes horizontally 0.05 units during hitstop.
- Scaling: light 3 to 5 frames, heavy 7 to 10, finishers and parries 12.

### 6.5 Parry

- Startup 0 frames, active 6 frames, recovery 20 frames on whiff.
- On success: enemy enters 40 frame stagger, 12 frame hitstop, ink splatter, player recovery cancelled, ink meter +20.
- Parryable attacks flagged in enemy AttackData. Some boss attacks are unparryable and flash red during startup.

### 6.6 Ink Meter

- Max 100. Gained by hitting (per AttackData) and parrying.
- Spend 50: weapon special.
- Spend 100: Redraw heal (restores 30% health, 45 frame vulnerable animation).
- Decays by 1 per second after 5 seconds without hitting anything. Encourages aggression.

### 6.7 Player Health and Damage

- Player health: 100. Invulnerability after being hit: 45 frames, flicker effect.
- Enemy contact does not damage the player (avoids cheap hits). Only attacks do.

## 7. Weapons

Each weapon is a `WeaponData` ScriptableObject with a full move list: Light1, Light2, Light3, Heavy, UpAttack, AirLight, AirHeavy, DownAir, Special.

| Weapon | Speed | Range | Identity | Special (50 ink) |
|---|---|---|---|---|
| Brush Katana | Fast | Medium | Wide smear arcs, balanced | Ink wave projectile |
| Ruler Greatsword | Slow | Long | Big hitstop, super armor on heavy | Overhead slam that creates shockwave |
| Compass Spear | Medium | Long, narrow | Thrusts, can plant and pole-vault | Multi-thrust flurry |
| Paperclip Chain | Medium | Long, arcing | Whip, can grab ledges and pull enemies | Spin that pulls enemies in |
| Scribble Gauntlets | Very fast | Short | Highest DPS, rapid jabs | Scribble barrage |

Brush Katana moveset (implemented): a four-hit light string, Light 1 > 2 > 3 > 4 (descending cut, rising cut, stepping thrust, sweeping finisher), and a different heavy branching off each of the first three lights: Light 1 > Iaido lunge, Light 2 > Rising Moon (launcher), Light 3 > Falling Blossom (leaping cleave). Heavy from neutral is an overhead cleave.

Vertical slice: Brush Katana only.

Weapon swap: equip at checkpoints, not mid-combat (keeps balance manageable). Revisit later.

## 8. Abilities (Unlocked by Bosses)

Stored in `AbilityUnlocks` save data as flags. Code checks flags before allowing the state.

| Ability | Source | Combat use | Traversal use |
|---|---|---|---|
| Dash (Eraser) | Starting ability in slice, later tied to Boss 1 | 8 frame i-frames, cancel tool | Phase through eraser walls |
| Grapple Line (Pen Stroke) | Boss 1: Highlighter | Pull light enemies in | Swing from anchor points |
| Double Jump (Spring Doodle) | Boss 2: Stapler Titan | Small hitbox under player | Reach high ledges |
| Ground Pound (Stamp) | Boss 3: Spiral | Bounce enemies, AoE | Break weak floors |
| Wall Cling (Tape) | Boss 4: Crossout | Wall-jump attacks | Vertical sections |
| Carbon Copy | Post-Crossout reward | Echo repeats last 60 frames of attacks at 50% damage | Trigger two switches at once |

Dash defaults: 10 frames, 16 units/s, 8 frames invulnerable, 1 air dash per airtime, 20 frame ground cooldown.

## 9. Enemies

Standard enemies built on `EnemyBase` with a state machine: Idle, Patrol, Alert, Approach, Attack, Hitstun, Launched, Dead.

| Enemy | Behavior | Purpose |
|---|---|---|
| Doodle Grunt | Walks, 1 slow swing | Teaches basic combat |
| Scribble Bat | Flies, dive attacks | Teaches air attacks |
| Pencil Lancer | Long telegraphed thrust | Teaches parry |
| Tack Turret | Stationary, fires projectiles | Teaches dash through |
| Eraser Crawler | Deletes platform tiles it walks over | Pressure and routing |

Rules:

- Every enemy attack has at least 12 frames of visible startup (telegraph). Unfair hits destroy game feel.
- Max 2 enemies attacking the player at once (attack token system). Others circle and wait.

## 10. Bosses

Built on `BossBase` with a list of `BossPhase` assets. Each phase has an attack pattern pool, health threshold, and phase-transition cinematic (short, skippable after first view).

| # | Boss | World | Core test | Phase 2 twist (at 50% HP) |
|---|---|---|---|---|
| 1 | The Highlighter | Lined Notebook | Parrying and reaction | Floods lower arena with glowing ink; must stay on raised platforms |
| 2 | Stapler Titan | Graph Paper | Positioning, punishing openings | Staples pin platforms in place and remove others |
| 3 | The Spiral | Blueprint | Dash timing | Arena slowly pulls player toward center vortex |
| 4 | The Crossout | Crumpled Page | Mastery of own kit | Mirrors player's unlocked abilities |
| 5 | Wet Ink Hydra (optional) | Wet Page | Target priority | Heads regrow if not finished with a heavy |
| Final | The Eraser | Spiral Binding | Everything | Deletes arena floor; final phase on blank page, player uses grapple to create platforms |

Boss design rules:

- Health bar always visible. Phase change at 50%, optional third phase at 20% for final boss.
- Every attack is learnable: consistent telegraph animation and sound per attack.
- Never more than 2 attack types active at once.
- Target fight length: 2 to 4 minutes for a first clear.
- Instant retry: under 3 seconds from death to fight restart.

Vertical slice: The Highlighter.

## 11. Level Design

### 11.1 Structure

- Rooms connected by transitions (Hollow Knight style), not one big scene.
- Build rooms with Unity Tilemap plus a line-art tileset (drawn as simple geometric ink lines, easy to make).
- Each room is a prefab with a Cinemachine confiner, spawn points, and exits.

### 11.2 Worlds

| World | Paper type | New mechanic |
|---|---|---|
| 1 | Lined Notebook | Tutorial; lines are one-way platforms |
| 2 | Graph Paper | Grid-snapped moving blocks |
| 3 | Blueprint | Conveyors, pistons, gears |
| 4 | Crumpled Page | Gravity shifts at fold lines |
| 5 | Wet Page | Ink flows down as a hazard; slippery surfaces |
| 6 | Spiral Binding | Vertical climb through metal rings, final boss |

### 11.3 Visual Layers

Background paper texture, faint notebook lines (parallax 0.9), midground doodles (parallax 0.6), gameplay layer, foreground smudges (parallax 1.1). Only the gameplay layer is fully black ink; other layers are lighter grey so gameplay reads clearly.

### 11.4 Checkpoints

Ink pot checkpoints: restore health, save, allow weapon swap. Placed before every boss and roughly every 3 to 4 rooms.

## 12. Camera

- Cinemachine with slight look-ahead in movement direction (0.5 units).
- Dead zone: small horizontal, larger vertical (avoid camera bobbing on every jump).
- Screen shake via Cinemachine Impulse. Default amplitudes: light hit 0.05, heavy 0.15, boss slam 0.3. Include a screen shake intensity setting (0 to 100%) in options for accessibility.

## 13. Audio (lightweight)

- Pen scratch for footsteps, paper whoosh for swings, wet ink splat for hits.
- Hit sounds pitch-randomized by plus or minus 5% to avoid repetition.
- Music: per-world loop, boss track with layer that intensifies at phase 2.
- Use free libraries (freesound.org with license check) or simple generated sounds initially.

## 14. UI

- HUD: health (top-left), ink meter below it, boss bar (bottom-center during fights).
- Menus: Main, Pause, Options (audio, screen shake, controls rebinding, display), Weapon select at checkpoints.
- Style: hand-drawn boxes, uneven lines, paper background.

## 15. Save System

- JSON save file in `Application.persistentDataPath`.
- Stores: unlocked abilities, unlocked weapons, equipped weapon, last checkpoint, defeated bosses, options.
- 3 save slots. Autosave at checkpoints only.

## 16. Milestones

Each milestone ends with a playable build and a commit tagged m1, m2, etc.

### Milestone 1: Movement Feel (target: 2 to 3 weeks)

- Project setup, Git, folder structure, CLAUDE.md
- Input System with keyboard and gamepad, InputBuffer
- KinematicBody2D and all movement from Section 5
- Placeholder capsule player (no rig yet)
- Debug tools from Section 3.3
- One gym scene: flat ground, slopes, one-way platforms, walls, gaps

**Acceptance:** Coyote time, jump buffer, variable jump, fast fall, and corner correction all verifiable with frame-step. Movement feels good with a capsule. If it does not feel good now, stop and tune before moving on.

### Milestone 2: Stick Figure Rig and Pose Tools (2 to 3 weeks)

- StickFigureRig, PoseData, PoseClip, PoseAnimator
- Scene view pose editor tool
- Poses for idle, run cycle (6 poses), jump, fall, land, dash
- Replace capsule with rig

**Acceptance:** Developer can create and save a new pose in under 2 minutes using the editor tool.

### Milestone 3: Combat Core (3 to 4 weeks)

- Hitbox/hurtbox system, AttackData, hit resolver
- Brush Katana full moveset
- Cancel system, hitstop, screen shake, smear frames, trails, ink splatter
- Parry and ink meter
- Training dummy that logs damage and shows combo counter

**Acceptance:** A 5+ hit combo is possible using only cancel rules. Hitboxes match visuals in slow motion.

### Milestone 4: Enemies (2 to 3 weeks)

- EnemyBase, attack token system
- Doodle Grunt, Scribble Bat, Pencil Lancer
- Player hitstun, death, respawn

**Acceptance:** Fighting 3 mixed enemies feels fair; every enemy attack is readable.

### Milestone 5: Vertical Slice (3 to 4 weeks)

- 6 to 8 rooms of World 1 with transitions and a checkpoint
- The Highlighter boss, both phases
- HUD, pause menu, basic audio
- Grapple Line unlock after boss

**Acceptance:** 10 to 15 minute playable slice. Give it to 3 people who have not seen it; watch without helping; note where they get stuck or frustrated.

**Decision point after M5:** Is the core loop fun? If not, iterate on M1 to M3 systems before building content.

### Milestone 6: Save System, Menus, Options (1 to 2 weeks)

### Milestone 7: Content Production (bulk of development)

- Worlds 2 to 6, one at a time, each with its boss, ability, and enemies
- Remaining 4 weapons

### Milestone 8: Polish

- Ink wobble shader, paper texture pass, particle polish
- Controller rumble, full audio pass, accessibility options
- Performance: hold 60+ fps on integrated graphics

### Milestone 9 (optional): Local 1v1 Arena Mode

- Two players, shared keyboard or two gamepads, stock-based. Online play is out of scope.

## 17. Testing

- EditMode tests: input buffer timing, frame-data math, damage calculation, save/load round trip.
- PlayMode tests: jump reaches configured height within 0.05 units; coyote jump succeeds on frame 6 and fails on frame 7.
- Manual feel checklist run after every movement or combat change.

## 18. Out of Scope (do not build unless explicitly asked)

- Online multiplayer or rollback netcode
- Character select roster
- Procedural level generation
- Story cutscenes beyond short boss intros
- Mobile or console ports

## 19. First Session Prompt for Claude Code

> Read MARGIN_Project_Outline.md. Create CLAUDE.md from Sections 0, 2, 3, and 4. Then begin Milestone 1: set up the folder structure, Input System, and InputBuffer with unit tests. Stop after the InputBuffer tests pass and summarize what you built before continuing.
