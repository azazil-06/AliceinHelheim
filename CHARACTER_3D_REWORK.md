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

## 2. Architectural Design: 2.5D Sidescroller

Instead of rewriting the entire game physics to full 3D, the project utilizes a **strict 2.5D sidescroller pattern**:

* **Physics & Colliders (2D):**
  * `Rigidbody2D` and `CapsuleCollider2D` remain active on the root `Player` GameObject.
  * Ensures 100% backward and forward compatibility with the procedural leg IK system (`SpyderBehaviour.cs` & `LegMover.cs`).
  * Seamlessly collides with existing terrain and platforms on **Layer 14 (`base`)**.
  * Eliminates Z-axis drift / slipping issues without needing rigid constraint hacks.

* **Strict Sidescroller Facing (Left & Right Only):**
  * The character is strictly binary in facing direction:
    * **Facing Right (+X):** Exact yaw of `90°`.
    * **Facing Left (-X):** Exact yaw of `270°` (`-90°`).
  * **Idle Persistence:** When the player stops moving, the character remains facing in whichever direction it last moved (Left or Right). It never reverts to facing forward (toward the camera) or backward.
  * `cameraFacingTilt` is set to `0°` by default for a clean, authentic sidescroller silhouette, with responsive fast turning (`turnSpeed = 24`).

---

## 3. Core Script Improvements

### `PlayerController.cs`
Located at: `Assets/Scripts/PlayerController.cs`

1. **3D Visual Root & Sidescroller Yaw Turning:**
   * **`visualRoot` (Transform):** Child holding the 3D model.
   * **`turnSpeed` (float, default `24`):** Fast, snappy turning between Left and Right.
   * **`cameraFacingTilt` (default `0°`):** Set to `0°` for strict 90° / 270° sidescroller orientation.
   * **`smoothTurning` (bool):** Interpolation toggle.

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
| **`Run`** | `Assets/Animation/Running.fbx` | Humanoid, Looping (`loopTime = true`), Root motion locked | `Speed > 0.1` |
| **`Jump`** | *(Ready for Jump clip)* | Humanoid | `Jump` trigger |
| **`Fall`** | *(Ready for Fall clip)* | Humanoid | `IsFalling == true` |
| **`Attack`**| *(Ready for Attack clip)* | Humanoid | `Attack` trigger |

* **How Left/Right Motion Plays:**
  * When pressing **D** (Right), `PlayerController` points the 3D model right (90°) and sets `Speed > 0.1`, playing the looping `Run` clip to the right.
  * When pressing **A** (Left), `PlayerController` points the 3D model left (270°) and sets `Speed > 0.1`, playing the looping `Run` clip to the left.
  * When stopping, the character enters `Idle` and stays facing whichever direction you were last moving.

---

## 5. Unity Editor Tools (`Tools > Alice in Helheim`)

* **`Apply Blender Character to Player (With Textures)`**: Automatically attaches the 3D character under `Player/Visual`, maps the extracted textures/materials (`paint_basecolor.png`), links `Player3D.controller`, and disables the 2D sprite.
* **`Toggle 2D Sprite vs 3D Model`**: Instant 1-click toggle to alternate between the 2D sprite visual and the 3D model without breaking scene state.
* **`Generate 3D Animator Controller`**: Generates or updates `Assets/Animation/Player3D.controller`.
