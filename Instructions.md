WHAT'S IMPLEMENTED (For Agents)

---

## Project Overview

| Field | Value |
|---|---|
| **Engine** | Unity 6000.5.9f1 |
| **Pipeline** | URP (Universal Render Pipeline), 2D Renderer |
| **Active Scene** | `Assets/Scenes/DeadEnd.unity` |
| **Active Branch** | `feat/3d-character-rework` → `origin/feat/3d-character-rework` |
| **Git Remote** | `https://github.com/azazil-06/AliceinHelheim.git` |
| **Physics** | 2D (Rigidbody2D, CapsuleCollider2D) — 3D character rendered in 2.5D sidescroller layout |

---

## Scene Hierarchy — `DeadEnd.unity`

```
DeadEnd (Scene Root)
├── Player                           [Layer 13, Tag: Player]
│   ├── Components: Transform, Rigidbody2D, CapsuleCollider2D, PlayerController
│   ├── Position: (16.2, 2.52, 1.39), Scale: (3.41, 3.41, 3.41)
│   ├── Rigidbody2D: Freeze Rotation Z, gravityScale=1
│   ├── CapsuleCollider2D: size=(0.22, 0.88), offset=(0, 0.42)
│   └── Visual                       [local rotation: Y=105°]
│       └── Character                [Animator: Player3D.controller, applyRootMotion=false, cullingMode=AlwaysAnimate]
│           └── [Humanoid SkinnedMeshRenderer, updateWhenOffscreen=true]
│
├── Ground / Level Geometry          [Layer 14 "base"]
│   ├── base_3_0, base_3_0 (1), base_3_0 (2)   [EdgeCollider2D, ~3.8° slope, Y ∈ [2.19, 2.63]]
│   ├── Left Bound                   [BoxCollider2D, vertical wall]
│   └── Right bound                  [BoxCollider2D, vertical wall]
│
├── Spyder (Enemy)                   [Root bone hierarchy]
│   ├── Components: SpyderBehaviour, SpriteRenderer (sprite-based, .psd)
│   ├── Legs: Leg1–Leg8              [LegMover on each]
│   └── IK Targets: Front-F-IK_Target, Front-B-IK_Target, Hind-B-IK_Target, etc.
│
├── Background (Parallax Layers)     [Multiple GameObjects with ParallaxBackground]
│   └── Each: SpriteRenderer + ParallaxBackground, cam reference = Main Camera
│
├── Firefly Emitter                  [ParticleSystem + FireflyFollower]
│
├── UI Canvas
│   └── VignettePanel                [Image + PlayerVignette, fullscreen UI overlay]
│
└── Main Camera                      [Standard URP camera]
```

---

## Scripts — Full Reference

### `Assets/Scripts/PlayerController.cs` (589 lines)

**Class:** `PlayerController : MonoBehaviour`  
**Attach to:** `Player` root GameObject.

#### Responsibilities
- Input reading via `InputSystem_Actions` (new Unity Input System)
- 2.5D ground detection (CircleCast + ContactFilter2D normal angle filter)
- 2D physics movement (Rigidbody2D direct velocity write)
- Variable-height jump (coyote time + low-jump multiplier)
- 3-hit combo combat system + running kick
- Smooth vs instant 180° yaw flipping of `Visual` child
- Animator bridge with parameter caching (safe set methods)

#### Key Serialized Fields
| Field | Default | Purpose |
|---|---|---|
| `playerAnimate` | — | Auto-found Animator under `Visual` |
| `visualRoot` | — | The `Visual` child Transform |
| `moveSpeed` | 5 | Horizontal speed (m/s) |
| `jumpMultiplier` | 6 | Jump impulse force |
| `lowJumpMultiplier` | 2.5 | Extra gravity on ascending without jump held |
| `idleRightYaw` | 105° | Visual Y rotation when idle + facing right |
| `moveRightYaw` | 90° | Visual Y rotation when moving/jumping/attacking right |
| `leftYaw` | 270° | Visual Y rotation for all left-facing states |
| `turnSpeed` | 30 | Slerp speed for minor angle adjustments |
| `smoothTurning` | false | Smooth all turns (off = instant 180° flip) |
| `groundLayer` | Layer 14 | LayerMask for ground raycasts |
| `groundCheckDistance` | 0.15 | Downward probe while grounded |
| `groundCheckRadius` | 0.2 | Circle cast radius |
| `groundCheckOffset` | 0.05 | Origin offset inside capsule bottom |
| `combatIdleDuration` | 3s | Time in Fighting Idle before returning to normal |
| `comboWindow` | 0.9s | Attack chain window |
| `runningKickLunge` | 18 | Running attack impulse |
| `boxingLunge` | 6 | Boxing punch impulse |
| `kickLunge` | 12 | Kick finisher impulse |
| `lungeFriction` | 5 | Velocity bleed-down during attack |
| `attackRange` | 2.4 | Hit detection radius |
| `attackKnockback` | 6.5 | Enemy knockback force |
| `hitImpactDelay` | 0.16s | Delay from attack trigger to hit check |

#### Ground Check Design
- `ContactFilter2D.SetNormalAngle(35°, 145°)` — only walkable floors count; vertical walls (`Left Bound`, `Right bound`) are excluded
- **Liftoff guard:** `Time.time - lastJumpTime < 0.15s` → `isGrounded = false` immediately after jumping
- **Ascending guard:** `velocity.y > 0.25` → never re-grounds while rising
- **Hysteresis:** while airborne, probe depth shrinks to `0.04m` (vs `0.15m` when grounded) — prevents snapping to Run mid-short-hop

#### Combat Combo
```
Attack (stationary):  Boxing 1 → Boxing 2 → Kick  (AttackIndex: 0 → 1 → 2)
Attack (while moving): Run to Kick                  (AttackIndex: 3)
```
Combo advances only within `comboWindow` (0.9s). After `combatIdleDuration` (3s) with no attack, `inCombat=false` resets to normal Idle.

#### 2.5D Yaw Logic
```
facing==1 (right):
  idle, grounded, not attacking → idleRightYaw (105°)
  otherwise (moving/jump/combat) → moveRightYaw (90°)
facing==0 (left):
  → leftYaw (270°)
```
- Minor angle (< 65°) differences always Slerp smoothly (idle↔move on right side)
- Large angle (≥ 65°) with `smoothTurning=false` → instant snap (crisp 180° flip L↔R)

#### Public API
| Method | Use |
|---|---|
| `ResolveActiveAnimator()` | Auto-find Animator under Visual; call after hierarchy changes |
| `CacheAnimatorParameters()` | Refresh parameter cache; called by Resolve |
| `SetVisualRoot(Transform)` | Re-link visual when character model is swapped |
| `LockMovement()` | Animation event: freeze player mid-attack |
| `UnlockMovement()` | Animation event: unfreeze player |

---

### `Assets/Scripts/SpyderBehaviour.cs` (862 lines)

**Class:** `SpyderBehaviour : MonoBehaviour`  
**Attach to:** Spider root bone (`Bone 1`).

#### Responsibilities
Spider enemy AI with full autonomous behaviour state machine.

#### State Machine (priority order, `Update`)
```
_isHurt          → frozen, bleed knockback
_isPouncing      → leap arc flight
_isPounceTelegraph → crouch crouch → then LaunchPounce()
isAttacking      → front-leg strike animation (IK-driven)
_isFollowing     → CheckAttackTrigger / CheckPounceTrigger / MoveTowardPlayer
idle             → breathe, twitch
```

#### Key Features
| Feature | Details |
|---|---|
| **Ground conformance** | Raycast from above, lerp Y to `rideHeight` above ground; custom gravity when no floor |
| **Skitter cadence** | Burst (skitterSpeed=7.2) ↔ freeze (pauseDuration) alternation within `burstDistance=4.5` |
| **Stalk mode** | Beyond `burstDistance`, creeps at `stalkSpeed=3.0` |
| **Pounce/Leap** | Telegraph crouch → arc jump (sin curve, `pounceArcHeight=1.3`) → land → StartAttack |
| **Front-leg attack** | 3-phase IK animation: rear-up (38%) → slam-down (30%) → retract (32%) |
| **Overhead avoidance** | Does not flip when player is > `overheadHeightThreshold` above |
| **Hit flash** | All `SpriteRenderer[]` lerp from `hitFlashColor` back to original over `hurtDuration` |
| **Posture tilt** | Forward lean `forwardLeanAngle`, player aim pitch `aimPitchAngle`, nervous twitches |
| **Breathing** | Idle body bob via `breathAmplitude` sin wave |

#### Public API
| Method | Signature | Use |
|---|---|---|
| `TakeHit` | `(float knockbackForce, Vector2 dir)` | Called by `PlayerController.PerformAttackHitCheck` |

#### Key Properties (read from `LegMover`)
- `isAirborne` — read by `LegMover` to trigger airborne leg tuck
- `isAttacking` — `LegMover.isAttacking` set true on front legs during attack

---

### `Assets/Scripts/LegMover.cs` (342 lines)

**Class:** `LegMover : MonoBehaviour`  
**Attach to:** Each `Leg1`–`Leg8` GameObject under the spider body.  
**Execution Order:** `-10` (runs before IKManager2D at order 0).

#### Responsibilities
Procedural IK-driven leg locomotion for the spider enemy.

#### Gait System
- Legs divided into **Group 0** and **Group 1** (alternating tetrapod)
- `CanGroupStep()` — a group only steps when the other group is not mid-step
- Static counters `s_group0Stepping` / `s_group1Stepping` track live step counts
- `[RuntimeInitializeOnLoadMethod]` resets counters on domain reload

#### Step Algorithm (`LateUpdate`)
1. **Attack override** — if `isAttacking`, freeze foot in place (SpyderBehaviour controls IK target)
2. **Velocity smoothing** — exponential smooth `rawVelocity` for prediction
3. **Scale flip detection** — if `transform.lossyScale.x` sign changes → `Reanchor()` all feet
4. **Airborne check** — raycast below; if clearance > `airborneThreshold (0.38m)` → tuck feet upward with `Lerp`
5. **Step trigger** — if `|desired - anchor| > stepDistance` and group can step → `StartStep()`
6. **Step arc** — `SmoothStep(0,1,t)` interpolation + `Sin(t*π)*height` arc, speed scales 1× → 1.8× at high body speed

#### Key Fields
| Field | Default | Purpose |
|---|---|---|
| `ikTarget` | — | The `*_IK_Target` Transform this leg drives |
| `gaitGroup` | 0 | 0=Group A, 1=Group B |
| `stepDistance` | 0.35 | Displacement before triggering a step |
| `stepHeight` | 0.18 | Arc height of step |
| `stepSpeed` | 9 | Base step animation speed |
| `velocityPrediction` | 1 | How far ahead foot plants relative to velocity |
| `airborneThreshold` | 0.38 | Min clearance to trigger airborne tuck |
| `groundRayLength` | 5 | Raycast length for ground detection |

#### Public API
| Method | Use |
|---|---|
| `Reanchor()` | Re-snap foot to current rest position (called on flip, landing, end-attack, TakeHit) |

---

### `Assets/Scripts/ParallaxBackground.cs` (63 lines)

**Class:** `ParallaxBackground : MonoBehaviour`  
**Attach to:** Each background layer sprite.

#### Modes
- **`isFixed = true`:** Layer fixed to camera offset (Lerp smoothing)
- **`isFixed = false`:** Infinite-scroll parallax (`parallaxEffect` 0=static, 1=moves with cam); auto-tiles using `SpriteRenderer.bounds.size.x`

#### Key Fields
| Field | Purpose |
|---|---|
| `cam` | Reference to Main Camera |
| `parallaxEffect` | Scroll ratio vs camera |
| `smoothing` | Lerp speed (20 default) |

---

### `Assets/Scripts/PlayerVignette.cs` (177 lines)

**Class:** `PlayerVignette : MonoBehaviour` (`[ExecuteAlways]`)  
**Attach to:** A fullscreen `Image` UI element in the Canvas.

#### How It Works
- Creates a `Material` from `Custom/PlayerVignette` shader at runtime
- Each frame: converts player world position (+ `worldOffset`) to viewport UV, writes to `_PlayerViewportPos` shader property
- Shader draws a radial dark vignette centered on player in screen space
- `enablePulse` — subtle sin-wave breathing on `innerRadius`/`outerRadius`

#### Shader Properties Set
`_PlayerViewportPos`, `_InnerRadius`, `_OuterRadius`, `_MaxDarkness`, `_FadeExponent`, `_AspectRatio`, `_VignetteColor`

#### Key Fields
| Field | Default | Purpose |
|---|---|---|
| `innerRadius` | 0.28 | Clear visibility radius around player |
| `outerRadius` | 0.85 | Full darkness radius |
| `maxDarkness` | 0.75 | Darkness intensity (0–1) |
| `fadeCurve` | 1.4 | Gradient exponent |
| `enablePulse` | true | Atmospheric breathing effect |

---

### `Assets/Scripts/Particle follower.cs` (32 lines)

**Class:** `FireflyFollower : MonoBehaviour`  
**Attach to:** Firefly `ParticleSystem` emitter.

Smoothly follows player position (+ local `offset`) using `SmoothDamp`. `smoothTime=0.5` gives a gentle delayed drift feel.

---

### `Assets/Scripts/Editor/Player3DSetupWizard.cs` (~588 lines)

**Location:** Editor-only, not compiled into builds.  
**Menu:** `Tools > Alice in Helheim > ...`

#### Menu Items
| Menu Item | Function |
|---|---|
| `Apply Blender Character to Player (With Textures)` | Attaches 3D FBX to Player, maps materials, removes legacy 2D SpriteRenderer/Animator components |
| `Generate 3D Animator Controller` | Creates `Player3D.controller` at `Assets/Animation/` if not present |
| `Rebuild Full 3D Animator Controller` | Rebuilds all 9 states + transitions in-place (preserves GUID, does not overwrite asset) |
| `Setup 3D Character Prototype` | Creates `Visual` child with Unity mannequin as placeholder |

---

## Animation System — `Player3D.controller`

### States (9 total)

| State | Clip | Loop | Trigger / Condition |
|---|---|---|---|
| **Idle** | `Idle.fbx` | ✓ | Default; `Speed < 0.1 && IsGrounded && !InCombat` |
| **Run** | `Run_InPlace.anim` | ✓ | `IsMoving && IsGrounded` |
| **Fighting Idle** | `Fighting_Idle_InPlace.anim` | ✓ | `InCombat && !IsMoving && IsGrounded` |
| **Boxing 1** | `Boxing_1_InPlace.anim` | ✗ | `AttackTrigger && AttackIndex==0` |
| **Boxing 2** | `Boxing_2_InPlace.anim` | ✗ | `AttackTrigger && AttackIndex==1` |
| **Kick** | `Kick_InPlace.anim` | ✗ | `AttackTrigger && AttackIndex==2` |
| **Run to Kick** | `Run_to_kick_InPlace.anim` | ✗ | `AttackTrigger && AttackIndex==3` |
| **Jump** | `Jumping_InPlace.anim` | ✗ | `JumpTrigger && !IsRunningJump` |
| **Running Jump** | `Running_Jump_InPlace.anim` | ✗ | `JumpTrigger && IsRunningJump` |

### Animator Parameters

| Name | Type | Set by |
|---|---|---|
| `Speed` | Float | `|moveInput.x|` |
| `MoveSpeed` | Float | same as Speed |
| `IsGrounded` | Bool | `CheckGround()` result |
| `IsMoving` | Bool | `|moveInput.x| > 0.1` |
| `IsFalling` | Bool | `!isGrounded && velocity.y < -0.5` |
| `InCombat` | Bool | combat timer active |
| `AttackIndex` | Int | 0=Boxing1, 1=Boxing2, 2=Kick, 3=RunKick |
| `AttackTrigger` | Trigger | on attack press |
| `JumpTrigger` | Trigger | on jump press |
| `IsRunningJump` | Bool | `isMoving` at time of jump |

### Critical Transition Rules
- **All Run-entry transitions require `IsGrounded == true`** — prevents mid-air run animations
- `Run → Running Jump` on `JumpTrigger` while running
- `Jump/RunningJump → Run` on `IsGrounded && Speed > 0.1`
- `Jump/RunningJump → Idle/FightingIdle` on `IsGrounded && Speed < 0.1`
- Combat attacks all return to `Fighting Idle` or `Idle` on clip end

### Clips — In-Place Convention
All clips are **in-place** (no root motion). `applyRootMotion = false`. Horizontal velocity is 100% driven by `PlayerController` physics.

---

## Physics Setup

### Player
| Component | Settings |
|---|---|
| `Rigidbody2D` | `gravityScale=1`, `Freeze Rotation Z`, Continuous Collision Detection |
| `CapsuleCollider2D` | size=(0.22, 0.88), offset=(0, 0.42), Capsule direction: Vertical |

### Ground Layer (Layer 14, name: `base`)
| Object | Collider | Notes |
|---|---|---|
| `base_3_0`, `(1)`, `(2)` | `EdgeCollider2D` | Walkable floor, slight 3.8° slope, Y ∈ [2.19, 2.63] |
| `Left Bound`, `Right bound` | `BoxCollider2D` | Vertical walls, excluded by NormalAngle filter |

### Ground Check Filter
```csharp
ContactFilter2D:
  useTriggers = false
  layerMask   = Layer 14
  normalAngle = [35°, 145°]   // Only upward-facing normals (floors), not vertical walls
```

---

## Input System — `InputSystem_Actions.inputactions`

| Action | Keyboard | Gamepad | Notes |
|---|---|---|---|
| `Move` | WASD / Arrows | Left Stick, Joystick | X-axis only used (Y ignored) |
| `Jump` | Space | Button South | Coyote time 0.12s |
| `Attack` | X, Mouse LMB | Button West | Combo on rapid re-press |
| `Sprint` | Left Shift | Left Stick Press | (not yet used in PlayerController) |
| `Previous` | 1 | DPad Left | — |
| `Next` | 2 | DPad Right | — |

---

## Asset Organization

```
Assets/
│
├── Animation/
│   ├── Player3D.controller            # Mecanim controller (9 states)
│   ├── Idle.fbx                       # Idle source
│   ├── Running.fbx                    # Run source
│   ├── Jumping.fbx                    # Jump source
│   ├── Running Jump.fbx               # Running jump source
│   ├── Run_InPlace.anim               # In-place extracted from Running.fbx
│   ├── Jumping_InPlace.anim           # In-place extracted from Jumping.fbx
│   ├── Running_Jump_InPlace.anim      # In-place extracted
│   └── Combat/
│       ├── Boxing_1_InPlace.anim
│       ├── Boxing_2_InPlace.anim
│       ├── Kick_InPlace.anim
│       ├── Run_to_kick_InPlace.anim
│       └── Fighting_Idle_InPlace.anim
│
├── Background/                        # Parallax layer PNGs
│
├── Character/
│   ├── Character.fbx                  # Humanoid 3D rig (Blender export)
│   ├── Materials/                     # Character material assets
│   └── Textures/                      # Character texture maps
│
├── Materials/
│   ├── Leaf.mat
│   ├── Raindrop.mat
│   └── trail.mat
│
├── Procedural/
│   └── Spyder/
│       └── spyder_org.psd             # Spider enemy sprite (2D, PSD)
│
├── Scenes/
│   └── DeadEnd.unity                  # Only scene
│
├── Scripts/
│   ├── Editor/
│   │   └── Player3DSetupWizard.cs     # Editor tools (not in build)
│   ├── PlayerController.cs            # Player input, physics, combat, animation
│   ├── SpyderBehaviour.cs             # Spider AI state machine
│   ├── LegMover.cs                    # Procedural spider leg IK
│   ├── ParallaxBackground.cs          # Parallax / fixed background scroll
│   ├── PlayerVignette.cs              # Player-centered vignette shader controller
│   └── Particle follower.cs           # Firefly particle emitter follower
│
├── Settings/
│   ├── UniversalRP.asset              # URP pipeline asset
│   ├── Renderer2D.asset               # 2D renderer (URP)
│   └── Lit2DSceneTemplate             # Scene template
│
├── Shaders/
│   ├── PlayerVignette.shader          # Custom/PlayerVignette (radial dark vignette)
│   └── PlayerVignetteMat.mat          # Material using that shader
│
├── CHARACTER_3D_REWORK.md             # Implementation notes (rework guide)
├── InputSystem_Actions.inputactions   # Input action map
└── InputSystem_Actions.cs             # Auto-generated C# wrapper
```

---

## Git State

| Branch | Last Commit | Status |
|---|---|---|
| `feat/3d-character-rework` *(active)* | `db7bdb5` — fix: prevent mid-air run + wall ground filter | 1 ahead of origin |
| `main` | `fffea76` — rework checkpoint | Stable base |
| `procedural` | `fea5856` — procedural rigging and first anim clip | Archived |

### Recent Commits (feat/3d-character-rework)
```
db7bdb5  fix(character-3d): prevent mid-air run transitions and filter wall ground checks
04d0d7b  chore(character-3d): remove legacy 2D character setup, sprites, and compatibility fallback
e554139  fix(character-3d): align right idle yaw to 105 and restore in-place running jump
227dcd7  fix(animation): remove procedural ponytail physics and restore clean in-place combat locomotion
2c9c9f7  feat(3d-character): implement procedural soft-body ponytail physics
08c0a49  feat(3d-character): add combat combo system, fighting idle stance, dual jumps
5003b7b  fix(character-3d): resolve ground foot alignment and strict sidescroller facing
a26056a  fix(animation): convert Running clip to in-place and disable applyRootMotion
```

---

## Known Quirks & Constraints

1. **2.5D pattern:** Physics is strictly 2D (Rigidbody2D). The 3D character is purely cosmetic, rendered via URP 2D Renderer. Never add a Rigidbody3D or 3D Collider to Player.

2. **In-place animations:** All clips must have root motion zeroed or be configured as in-place. `applyRootMotion = false` on the Animator. Any clip with root motion will cause jerky teleporting.

3. **Idle right yaw is 105° not 90°:** The Idle.fbx has a slight leftward head/torso twist in the rig; 105° corrects for it so the character appears to face right symmetrically matching leftYaw=270°.

4. **Ground layer is 14 (`base`), not Default (0):** Always assign `groundLayer = 1 << 14` in inspector; never use Default.

5. **NormalAngle filter [35°, 145°]:** Walls are vertical (90° ± ~90°) and get correctly excluded. If you add new walkable geometry on a different angle, ensure its surface normal falls in this range.

6. **`ResolveActiveAnimator()` targets `visualRoot.GetComponentInChildren<Animator>()`:** It searches under `Visual` only, not from the Player root, to avoid finding any stray disabled Animator on Player root.

7. **`IdleToRun.cs` is deleted:** Was a legacy `StateMachineBehaviour` stub; it no longer exists. Do not recreate it.

8. **Procedural ponytail removed:** An earlier commit (`2c9c9f7`) added soft-body ponytail chain physics; it was reverted (`227dcd7`) as it looked bad. Do not re-add procedural ponytail.

9. **Spider is 2D sprite-based:** `SpyderBehaviour` uses `SpriteRenderer[]` for damage flash. The spider is NOT a 3D model.

10. **`LegMover` execution order = -10:** Must run before IKManager2D (order 0). Do not change execution order.







IMPROVEMENTS (For Claude Desktop)

---
