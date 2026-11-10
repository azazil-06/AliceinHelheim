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

## 3. Core Script Improvements

### `PlayerController.cs`
Located at: `Assets/Scripts/PlayerController.cs`

1. **Root Transform & 3D Visual Root:**
   * **`transform.rotation`:** Automatically locked to `Quaternion.identity` in `Awake()`.
   * **`visualRoot` (Transform):** Child holding the 3D model.
   * **`smoothTurning` (bool, default `false`):** Instant snappy flip between Left (270°) and Right (90°).
   * **`cameraFacingTilt` (default `0°`):** Pure 90°/270° sidescroller angles.
   * **`turnSpeed` (float, default `30`):** Fast interpolation if smooth turning is enabled.

2. **Accurate 2.5D Ground Check:**
   * Downward circle/collider cast against Layer 14 (`base`).
   * Configurable `groundCheckDistance`, `groundCheckRadius`, and `groundCheckOffset`.
   * Displays visual debug Gizmos in the Scene View (green = grounded, red = airborne).

3. **Dual Animator Compatibility:**
   * **Modern 3D Humanoid Parameters:**
     * `Speed` (Float) / `MoveSpeed` (Float)
     * `IsGrounded` (Bool)
     * `IsMoving` (Bool)
     * `IsFalling` (Bool)
     * `Jump` (Trigger)
     * `Attack` (Trigger)
   * **Legacy 2D Sprite Parameters:**
     * `Motion` (Float)
     * `Facing` (Float: 1 = Right, 0 = Left)
     * `isRunning` (Bool)
     * `isGrounded` (Bool)
     * `falling` (Bool)
     * `isJumping` (Trigger)

4. **Combat & Sledgehammer:**
   * Attack lunges immediately snap the model's orientation towards the strike direction (Left or Right).
   * Hit detection against `SpyderBehaviour` remains fully operational.

---

## 4. Animation Setup (`Player3D.controller`)

The 3D state machine is located at: `Assets/Animation/Player3D.controller`

| State | Animation Clip Source | Settings | Trigger Condition |
|---|---|---|---|
| **`Idle`** | `Assets/Animation/Idle.fbx` | Humanoid, Looping (`loopTime = true`) | `Speed < 0.1` (Default State) |
| **`Run`** | `Assets/Animation/Run_InPlace.anim` | Humanoid, Looping (`loopTime = true`), `RootT.z` stripped for smooth in-place locomotion | `Speed > 0.1` |
| **`Jump`** | *(Ready for Jump clip)* | Humanoid | `Jump` trigger |
| **`Fall`** | *(Ready for Fall clip)* | Humanoid | `IsFalling == true` |
| **`Attack`**| *(Ready for Attack clip)* | Humanoid | `Attack` trigger |

* **How In-Place Running Eliminates Jerkiness:**
  * Raw FBX animations often include root translation drift (+3.01m per cycle in `Running.fbx`).
  * `Run_InPlace.anim` zeroes the forward translation while preserving the natural hip bounce (`RootT.y`) and foot stride.
  * Rigidbody2D drives world motion at `moveSpeed = 10`, ensuring completely smooth, continuous movement with no snapping.

* **How Left/Right Motion Plays:**
  * When pressing **D** (Right), `PlayerController` points the 3D model right (90°) and sets `Speed > 0.1`, playing `Run_InPlace` to the right.
  * When pressing **A** (Left), `PlayerController` points the 3D model left (270°) and sets `Speed > 0.1`, playing `Run_InPlace` to the left.
  * When releasing the keys, the character enters `Idle` and stays facing whichever direction you were last moving.

---

## 5. Unity Editor Tools (`Tools > Alice in Helheim`)

* **`Apply Blender Character to Player (With Textures)`**: Automatically attaches the 3D character under `Player/Visual`, maps the extracted textures/materials (`paint_basecolor.png`), calibrates ground foot alignment and CapsuleCollider2D, links `Player3D.controller`, and disables the 2D sprite.
* **`Toggle 2D Sprite vs 3D Model`**: Instant 1-click toggle to alternate between the 2D sprite visual and the 3D model without breaking scene state.
* **`Generate 3D Animator Controller`**: Generates or updates `Assets/Animation/Player3D.controller`.
