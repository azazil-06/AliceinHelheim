# 3D Character Rework Guide (2.5D Sidescroller Architecture)

This document outlines the architecture, setup instructions, and workflows for transitioning the character from a 2D sprite to a 3D model in **Alice in Helheim**, while preserving all previous 2D progress.

---

## 1. Git Safety & Branch Isolation

* **Active Working Branch:** `feat/3d-character-rework`
* **Base Commit (Safe Point):** `fffea76 rework checkpoint` on branch `main`

### How to Switch Between 2D and 3D Branches
* **Switch back to the original 2D build at any time:**
  ```bash
  git checkout main
  ```
* **Return to the 3D experimental branch:**
  ```bash
  git checkout feat/3d-character-rework
  ```

---

## 2. Architectural Design: Strict 2.5D Sidescroller

Instead of rewriting the entire game physics to full 3D, the project utilizes a **strict 2.5D sidescroller pattern**:

* **Physics & Colliders (2D):**
  * `Rigidbody2D` and `CapsuleCollider2D` remain active on the root `Player` GameObject.
  * Root `Player.transform.rotation` is strictly enforced to `(0, 0, 0)` in `Awake()` to guarantee all 2D physics stay perfectly on the XY plane.
  * Ensures 100% backward and forward compatibility with the procedural leg IK system (`SpyderBehaviour.cs` & `LegMover.cs`).
  * Seamlessly collides with terrain and platforms on **Layer 14 (`base`)**.
  * Eliminates Z-axis drift / slipping issues without needing rigid constraint hacks.

* **Strict Sidescroller Facing (Left & Right Only):**
  * The character faces left (`-X`) or right (`+X`) in 2.5D sidescroller perspective:
    * **Idle Right (`idleRightYaw`):** `105°` yaw balances the Idle clip's slight leftward head/torso twist so the character faces cleanly right (`+X`), symmetrical with Idle Left.
    * **Move / Combat / Jump Right (`moveRightYaw`):** `90°` yaw for `Run`, `Jump`, `Running Jump`, and combat states facing right (`+X`).
    * **Left (`leftYaw`):** `270°` (`-90°`) yaw across all states facing left (`-X`).
  * **Turn & Blend Behavior:** Smoothly blends minor angle adjustments on the right (`105° <-> 90°`, `turnSpeed = 30f`) and snaps crisply (`smoothTurning = false`) between Left (`270°`) and Right (`90°`/`105°`) for responsive sidescroller controls.
  * **Idle Persistence:** When the player stops moving, the character remains facing in whichever direction it last moved (Left or Right). It never reverts to facing into the camera or wall.

* **Ground Foot Alignment:**
  * `Visual.localPosition` is calibrated to `(0f, -0.022f, 0f)`.
  * `CapsuleCollider2D` is calibrated to `offset = (0f, 0.42f)` and `size = (0.22f, 0.88f)`.
  * The bottom of the capsule collider coincides within 3mm of the shoe soles in both Idle and Run poses, placing the feet flush against the ground collider line (`y = 2.459`).

---

## 3. Combat Combo & Dual Jump System

### 1. 3-Hit Stationary Combo (Press `X` or Left Mouse Button)
When stationary (`Speed < 0.1`):
* **Hit 1:** `Boxing 1` (`Boxing_1_InPlace.anim`) — Fast left punch, `boxingLunge = 6f`, 0.9f reach.
* **Hit 2:** `Boxing 2` (`Boxing_2_InPlace.anim`) — Chained right cross if `X` is pressed within `comboWindow = 0.9s`.
* **Hit 3:** `Kick` (`Kick_InPlace.anim`) — Powerful finishing roundhouse/kick, `kickLunge = 12f`, 1.1x reach, increased knockback.
* Resets to `Boxing 1` after the finisher or if the combo window expires.

### 2. Running Kick (Press `X` while running)
* When moving (`Speed > 0.1` / `isMoving = true`), pressing `X` executes **`Run to kick`** (`Run_to_kick_InPlace.anim`, Combo Index 3).
* Applies an aggressive lunge impulse (`runningKickLunge = 18f`), delivering long-range gap-closing strikes against enemies.

### 3. Dynamic Fighting Idle Stance
* After executing any attack (boxing combo or running kick), the character enters **`Fighting Idle`** (`Fighting_Idle_InPlace.anim`).
* The character maintains an alert combat posture for **3.0 seconds** (`combatIdleDuration = 3.0f`).
* If no further attacks occur within this duration, the character smoothly eases back into standard **`Idle`**.
* **Zero Delay Interrupt:** If movement or jumping input is received during `Fighting Idle`, the character transitions immediately to `Run` or `Jump` without waiting for the animation or timer to finish.

### 4. Dual Jump Mechanics (Press `Spacebar`)
* **Stationary Jump (`Jump`):** When stationary (`Speed < 0.1` and `|vx| <= 0.5`), pressing Space triggers `Jump` (`Assets/Animation/Jumping_InPlace.anim`).
* **Running Jump (`Running Jump`):** When moving or carrying horizontal momentum (`isMoving || |vx| > 0.5`), pressing Space sets `IsRunningJump = true` and triggers `Running Jump` (`Assets/Animation/Running_Jump_InPlace.anim`).
* **Coyote Time & Liftoff Guard:** Includes a `0.12s` coyote-time window for uneven terrain slopes and a `0.15s` / ascending-velocity ground-check guard so liftoff frames cannot prematurely cancel the jump state, plus mid-air horizontal steering in `FixedUpdate()`.
* **Seamless Landing:** When `IsGrounded` becomes true upon contact with the terrain:
  * Transitions directly into `Run` if horizontal speed is maintained (`Speed > 0.1`).
  * Transitions into `Fighting Idle` if stationary and still within the 3-second combat timer.
  * Transitions into standard `Idle` if stationary and out of combat.

---

## 4. Animation State Machine Breakdown (`Player3D.controller` — 9 States)

Located at: `Assets/Animation/Player3D.controller`

| State | Animation Clip Source | Loop | Description / Transition Logic |
|---|---|---|---|
| **`Idle`** | `Assets/Animation/Idle.fbx` | Yes | Clean, continuous resting stance. |
| **`Run`** | `Assets/Animation/Run_InPlace.anim` | Yes | In-place forward run with zero forward root drift. Driven by Rigidbody2D velocity. |
| **`Fighting Idle`** | `Assets/Animation/Combat/Fighting_Idle_InPlace.anim` | Yes | In-place combat stance maintained for 3 seconds post-attack before returning to `Idle`. |
| **`Boxing 1`** | `Assets/Animation/Combat/Boxing_1_InPlace.anim` | No | Stationary combo opener (in-place). Chains into `Boxing 2` on next `AttackTrigger`. |
| **`Boxing 2`** | `Assets/Animation/Combat/Boxing_2_InPlace.anim` | No | Stationary combo second hit (in-place). Chains into `Kick` on next `AttackTrigger`. |
| **`Kick`** | `Assets/Animation/Combat/Kick_InPlace.anim` | No | Stationary combo finisher (in-place). Exits to `Fighting Idle`. |
| **`Run to kick`** | `Assets/Animation/Combat/Run_to_kick_InPlace.anim` | No | Sprinting attack (in-place). Triggered by `AttackTrigger` when `Speed > 0.1`. Exits to `Fighting Idle`. |
| **`Jump`** | `Assets/Animation/Jumping_InPlace.anim` | No | Stationary vertical jump (`AnyState -> Jump` when `JumpTrigger && !IsRunningJump`). |
| **`Running Jump`** | `Assets/Animation/Running_Jump_InPlace.anim` | No | Running forward leap (`AnyState -> Running Jump` when `JumpTrigger && IsRunningJump`). |

---

## 5. In-Place Motion Standardization

Raw Mixamo animations frequently contain significant forward root translation drift:
* **Raw `Running Jump.fbx`** drifted forward by **+4.18m** in `RootT.z`.
* **Raw `Run to kick.fbx`** contained a massive **+1.98m forward drift** in `RootT.z`.
* **Raw `Kick.fbx`** drifted forward by **+0.48m**.
* **Raw `Boxing 1.fbx`** drifted forward by **+0.45m**.
* **Raw `Boxing 2.fbx`** drifted forward by **+0.22m**.

In a 2.5D sidescroller where `Rigidbody2D` and `CapsuleCollider2D` govern physics:
1. Built-in animation root translation pushes the 3D visual mesh away from the 2D physics collider.
2. When the animation clip completes, the visual mesh abruptly snaps back to the collider position, causing jarring hitching/jerking.
3. Furthermore, script lunges (`playerRB.AddForce`) and jump arcs doubled with the animation displacement.

**The Fix:**
Locomotion, jump, and combat animations were duplicated into clean `_InPlace.anim` assets where `RootT.z` and `RootT.x` are flattened to zero. The visual mesh remains perfectly anchored to the `CapsuleCollider2D`, while `PlayerController` provides smooth, responsive, physics-driven movement, jumps, and lunges that properly collide with walls, slopes, enemies, and obstacles without visual distortion or snapping.

---

## 6. Mixamo Humanoid Importer Setup

Mixamo FBX files imported into Unity require Mecanim Humanoid retargeting:
* **Avatar Setup:** Set to `ModelImporterAvatarSetup.CreateFromThisModel`.
* Each clip creates its own native Humanoid Avatar (`Hips` hierarchy), allowing Mecanim to cleanly retarget motions onto the custom character model (`CharacterAvatar`).
* **Root Motion Handling:**
  * For `Running.fbx`, `Jumping.fbx`, `Running Jump.fbx`, and all `Combat/*.fbx` clips, forward displacement is stripped into `*_InPlace.anim` clips with `RootT.x` and `RootT.z` zeroed out so `Rigidbody2D` controls world translation.
  * `Animator.applyRootMotion` stays `false` (`cullingMode = AlwaysAnimate`), and `SkinnedMeshRenderer.updateWhenOffscreen = true`.

---

## 7. Unity Editor Tools (`Tools > Alice in Helheim`)

* **`Apply Blender Character to Player (With Textures)`**:
  Attaches the 3D character under `Player/Visual`, maps the extracted textures/materials (`Character_Body_Mat` & `Character_Eyes_Mat`), calibrates ground foot alignment and `CapsuleCollider2D`, sets default `Visual` yaw (`105°`), links `Player3D.controller`, and removes any legacy 2D character components.
* **`Generate 3D Animator Controller`**:
  Generates `Assets/Animation/Player3D.controller` if missing.
* **`Rebuild Full 3D Animator Controller (Combos & Jumps)`**:
  Reconstructs all 9 in-place states, clips, parameters, and combo/jump transitions from scratch.

---

## 8. Procedural Animation Removal

Per user feedback, procedural ponytail physics (`SoftBodyPonytail.cs`) was completely removed from the project:
* The character relies entirely on clean, author-rigged Humanoid animation clips from Mixamo.
* This eliminates visual oscillation, unnatural wobble during 180° turns, and jitter during high-velocity attacks.
* `PlayerController.cs` and `Player3DSetupWizard.cs` have been completely decoupled from procedural hair simulation.
* All scene GameObjects in `DeadEnd.unity` are clean of any dangling or missing scripts.

