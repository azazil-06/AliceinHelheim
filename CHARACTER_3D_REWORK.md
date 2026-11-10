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
  * The character is strictly binary in facing direction:
    * **Facing Right (+X):** Exact yaw of `90°`.
    * **Facing Left (-X):** Exact yaw of `270°` (`-90°`).
  * **Instant Crisp Turn (`smoothTurning = false`):** Turning snaps instantly between Left (270°) and Right (90°) for classic responsive sidescroller controls without turning into the background.
  * **Idle Persistence:** When the player stops moving, the character remains facing in whichever direction it last moved (Left or Right). It never reverts to facing into the camera or wall.
  * `cameraFacingTilt` is set to `0°` by default for a clean, authentic sidescroller silhouette.

* **Ground Foot Alignment:**
  * `Visual.localPosition` is calibrated to `(0f, -0.022f, 0f)`.
  * `CapsuleCollider2D` is calibrated to `offset = (0f, 0.42f)` and `size = (0.22f, 0.88f)`.
  * The bottom of the capsule collider coincides within 3mm of the shoe soles in both Idle and Run poses, placing the feet flush against the ground collider line (`y = 2.459`).

---

## 3. Combat Combo & Dual Jump System

### 1. 3-Hit Stationary Combo (Press `X` or Left Mouse Button)
When stationary (`Speed < 0.1`):
* **Hit 1:** `Boxing 1` — Fast left punch, `boxingLunge = 6f`, 0.9f reach.
* **Hit 2:** `Boxing 2` — Chained right cross if `X` is pressed within `comboWindow = 0.9s`.
* **Hit 3:** `Kick` — Powerful finishing roundhouse/kick, `kickLunge = 12f`, 1.1x reach, increased knockback.
* Resets to `Boxing 1` after the finisher or if the combo window expires.

### 2. Running Kick (Press `X` while running)
* When moving (`Speed > 0.1` / `isMoving = true`), pressing `X` executes **`Run to kick`** (Combo Index 3).
* Applies an aggressive lunge impulse (`runningKickLunge = 18f`), delivering long-range gap-closing strikes against enemies.

### 3. Dynamic Fighting Idle Stance
* After executing any attack (boxing combo or running kick), the character enters **`Fighting Idle`**.
* The character maintains an alert combat posture for **3.0 seconds** (`combatIdleDuration = 3.0f`).
* If no further attacks occur within this duration, the character smoothly eases back into standard **`Idle`**.
* **Zero Delay Interrupt:** If movement or jumping input is received during `Fighting Idle`, the character transitions immediately to `Run` or `Jump` without waiting for the animation or timer to finish.

### 4. Idle Cycling (`Idle` <-> `Idle Look Around`)
* When resting in normal `Idle`, the animation cycles naturally into **`Idle Look Around`** at the completion of an idle loop (`exitTime = 1.0f`).
* At the end of `Idle Look Around`, it smoothly loops back to `Idle`.
* **Immediate Responsiveness:** Any movement, jump, or attack input instantly cancels the looking-around animation and enters the requested state with zero lag.

### 5. Dual Jump Mechanics (Press `Spacebar`)
* **Stationary Jump:** Pressing Space while still triggers **`Jumping`** (clip: `Assets/Animation/Jumping.fbx`), playing a vertical airborne leap.
* **Running Jump:** Pressing Space while moving triggers **`Running Jump`** (clip: `Assets/Animation/Running Jump.fbx`), playing an athletic forward leap.
* **Seamless Landing:** When `IsGrounded` becomes true upon contact with the terrain:
  * Transitions directly into `Run` if horizontal speed is maintained.
  * Transitions into `Fighting Idle` if stationary and still within the 3-second combat timer.
  * Transitions into standard `Idle` if stationary and out of combat.

---

## 4. Animation State Machine Breakdown (`Player3D.controller`)

Located at: `Assets/Animation/Player3D.controller`

| State | Animation Clip Source | Loop | Description / Transition Logic |
|---|---|---|---|
| **`Idle`** | `Assets/Animation/Idle.fbx` | Yes | Default resting stance. Cycles to `Idle Look Around` at `exitTime = 1.0`. |
| **`Idle Look Around`** | `Assets/Animation/Idle Look Around.fbx` | No | Periodic flavor idle cycle. Cycles back to `Idle` at `exitTime = 1.0`. Interrupted immediately by movement/combat. |
| **`Run`** | `Assets/Animation/Run_InPlace.anim` | Yes | In-place forward run with zero forward root drift. Driven by Rigidbody2D velocity. |
| **`Fighting Idle`** | `Assets/Animation/Combat/Fighting Idle.fbx` | Yes | Combat-ready stance maintained for 3 seconds post-attack before returning to `Idle`. |
| **`Boxing 1`** | `Assets/Animation/Combat/Boxing 1.fbx` | No | Stationary combo opener. Chains into `Boxing 2` on next `AttackTrigger`. |
| **`Boxing 2`** | `Assets/Animation/Combat/Boxing 2.fbx` | No | Stationary combo second hit. Chains into `Kick` on next `AttackTrigger`. |
| **`Kick`** | `Assets/Animation/Combat/Kick.fbx` | No | Stationary combo finisher. Exits to `Fighting Idle`. |
| **`Run to kick`** | `Assets/Animation/Combat/Run to kick.fbx` | No | Sprinting attack. Triggered by `AttackTrigger` when `Speed > 0.1`. Exits to `Fighting Idle`. |
| **`Jump`** | `Assets/Animation/Jumping.fbx` | No | Stationary jump. AnyState -> `Jump` (`JumpTrigger` & `IsRunningJump == false`). |
| **`Running Jump`**| `Assets/Animation/Running Jump.fbx` | No | Moving jump. AnyState -> `Running Jump` (`JumpTrigger` & `IsRunningJump == true`). |

---

## 5. Mixamo Humanoid Importer Setup

Mixamo FBX files imported into Unity require Mecanim Humanoid retargeting:
* **Avatar Setup:** Set to `ModelImporterAvatarSetup.CreateFromThisModel`.
* Each clip creates its own native Humanoid Avatar (`mixamorig:Hips` hierarchy), allowing Mecanim to cleanly retarget motions onto the custom character model (`CharacterAvatar`).
* **Root Motion Handling:**
  * For `Running.fbx`, forward displacement is stripped into `Run_InPlace.anim` with `RootT.z` zeroed out so Rigidbody2D controls world translation.
  * Combat and Jump clips (`Boxing 1`, `Boxing 2`, `Kick`, `Run to kick`, `Fighting Idle`, `Jumping`, `Running Jump`) feature natural zero-displacement root motion and use `lockRootPositionXZ = true`.

---

## 6. Unity Editor Tools (`Tools > Alice in Helheim`)

* **`Apply Blender Character to Player (With Textures)`**:
  Attaches the 3D character under `Player/Visual`, maps the extracted textures/materials (`Character_Body_Mat` & `Character_Eyes_Mat`), calibrates ground foot alignment and CapsuleCollider2D, links `Player3D.controller`, and disables the 2D sprite.
* **`Toggle 2D Sprite vs 3D Model`**:
  Instant 1-click toggle to alternate between the 2D sprite visual and the 3D model without breaking scene state.
* **`Generate 3D Animator Controller`**:
  Generates `Assets/Animation/Player3D.controller` if missing.
* **`Rebuild Full 3D Animator Controller (Combos & Jumps)`**:
  Reconstructs all 10 states, clips, parameters, and combo/jump transitions from scratch.

---

## 7. Procedural Soft-Body Ponytail & Hair Physics (`SoftBodyPonytail.cs`)

Located at: `Assets/Scripts/SoftBodyPonytail.cs`

Because standard Humanoid Mecanim retargeting only animates core humanoid bones (Hips through Head), extra bones like the character's ponytail do not receive animation curves from Mixamo clips. The `SoftBodyPonytail` component breathes life into the character through real-time procedural physics:

* **Dual-Strand Articulated Chains:**
  * **Chain A (Upper Strand):** `Hair_A_01` -> `Hair_A_02` -> `Hair_A_03` -> `Hair_A_04` -> `Hair_A_04_end` (4 articulated segments).
  * **Chain B (Lower Strand):** `Hair_B_01` -> `Hair_B_02` -> `Hair_B_03` -> `Hair_B_04` -> `Hair_B_04_end` (4 articulated segments).
  * 197 skinned mesh vertices deform smoothly in real-time as the bones rotate.

* **Physical Simulation Model:**
  * **Verlet Integration:** Simulates momentum, fluid drag, and air resistance per node without numerical instability.
  * **Distance Constraint:** Preserves exact bone lengths to machine precision (0 stretch or tearing).
  * **Restoring Spring Force:** Elasticity pulls each joint back towards its animated rest shape (`stiffness = 0.38f`).
  * **Aerodynamic Velocity Drag:** Character movement velocity drags and streams the hair back when sprinting (`movementLag = 0.45f`).
  * **Jump Buoyancy:** Hair lags downwards on launch and floats weightlessly at jump peak / freefall (`jumpFloat = 0.8f`).
  * **Subtle Idle Sway:** Gentle natural breathing and air fluctuation when stationary (`swayFrequency = 1.6f`, `swayAmplitude = 0.012f`).

* **Sidescroller 180° Flip Recovery:**
  * When the character snaps facing direction between Left (270°) and Right (90°), `flipCompensation` smoothly rotates the simulated particles around the head pivot to the new rear side, creating a realistic hair whip rather than clipping across the face.

* **Head Anti-Penetration Sphere:**
  * Spherical collider centered at the skull (`radius = 0.22f`) pushes hair nodes outwards so they never penetrate the back of the head or neck.

* **Visceral Combat Recoil Integration:**
  * `PlayerController.cs` automatically communicates with `SoftBodyPonytail.AddImpulse()` during attacks:
    * Boxing punches deliver a crisp reactive whip (`3.5f`).
    * Kick finisher delivers a sharp physical snap (`6.0f`).
    * Running kick delivers a long trailing lunge impulse (`8.0f`).
    * Leaping applies downward recoil (`4.0f`).

