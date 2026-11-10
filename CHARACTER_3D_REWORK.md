# 3D Character Rework Guide (2.5D Architecture)

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

## 2. Architectural Design: The 2.5D Pattern

Instead of rewriting the entire game physics to full 3D, the project utilizes a **2.5D architecture**:

* **Physics & Colliders (2D):**
  * `Rigidbody2D` and `CapsuleCollider2D` remain active on the root `Player` GameObject.
  * Ensures 100% backward and forward compatibility with the procedural leg IK system (`SpyderBehaviour.cs` & `LegMover.cs`).
  * Seamlessly collides with existing terrain and platforms on **Layer 14 (`base`)**.
  * Eliminates Z-axis drift / slipping issues without needing rigid constraint hacks.

* **Visuals & Animation (3D):**
  * The root `Player` GameObject stays aligned to world axes.
  * A child GameObject (`Visual`) hosts the 3D model, its `Animator`, and mesh/materials.
  * `PlayerController` manages the yaw rotation of the `Visual` child independently from the physics body.

---

## 3. Core Script Improvements

### `PlayerController.cs`
Located at: `Assets/Scripts/PlayerController.cs`

1. **3D Visual Root & Yaw Turning:**
   * **`visualRoot` (Transform):** Child holding the 3D model. If unassigned, automatically detects a child named `"Visual"` or any child with an `Animator`.
   * **`turnSpeed` (float):** Smooth interpolation speed when turning left/right.
   * **`cameraFacingTilt` (degrees, default `15°`):** In 2.5D, facing pure 90° produces a razor-flat side profile. Tilting slightly toward the camera (e.g., 75° right, 285° left) gives depth, revealing facial features, armor, and weapon details to the orthographic/perspective camera.
   * **`smoothTurning` (bool):** Toggle between smooth slerp rotation and instant snap.

2. **Accurate 2.5D Ground Check:**
   * Replaced the previous dummy `y > 4f` check with a downward circle/collider cast against Layer 14 (`base`).
   * Configurable `groundCheckDistance`, `groundCheckRadius`, and `groundCheckOffset`.
   * Displays visual debug Gizmos in the Scene View (green = grounded, red = airborne).

3. **Dual Animator Compatibility:**
   * Dynamically inspects available parameters on the active Animator to avoid missing parameter warnings.
   * **Modern 3D Humanoid Parameters:**
     * `Speed` (Float) / `MoveSpeed` (Float)
     * `IsGrounded` (Bool)
     * `IsMoving` (Bool)
     * `IsFalling` (Bool)
     * `Jump` (Trigger)
     * `Attack` (Trigger)
   * **Legacy 2D Sprite Parameters:**
     * `Motion` (Float)
     * `Facing` (Float)
     * `isRunning` (Bool)
     * `isGrounded` (Bool)
     * `falling` (Bool)
     * `isJumping` (Trigger)

4. **Combat & Sledgehammer:**
   * Attack lunges immediately snap the model's orientation towards the strike direction.
   * Hit detection against `SpyderBehaviour` remains fully operational.
   * Optional `weaponSocket` reference for attaching weapons to the 3D rig's hand bone.

---

## 4. Unity Editor Tools (`Tools > Alice in Helheim`)

An Editor wizard script is located at: `Assets/Scripts/Editor/Player3DSetupWizard.cs`

Open the top menu bar in Unity to access:

| Menu Item | Action |
|---|---|
| **Setup 3D Character Rig (Active Scene)** | Automates full setup on the `Player` GameObject in the active scene: creates the `Visual` child, builds a 3D prototype mannequin (body, visor direction marker, and sledgehammer with weapon socket), links `Player3D.controller`, sets `visualRoot`, and disables the 2D `SpriteRenderer` non-destructively. |
| **Toggle 2D Sprite vs 3D Model** | Instant 1-click toggle to alternate between the 2D sprite visual and the 3D model without breaking scene state. |
| **Generate 3D Animator Controller** | Automatically generates `Assets/Animation/Player3D.controller` with configured `Idle`, `Run`, `Jump`, `Fall`, and `Attack` states and transitions. |

---

## 5. How to Import and Use Your Own 3D Model

When you are ready to import your final 3D character (FBX, OBJ, or Blender file):

1. **Import the 3D Model:**
   * Drag your `.fbx` model into `Assets/Models/` (or your preferred folder).
   * In the FBX Import Inspector:
     * **Rig Tab:** Set *Animation Type* to **Humanoid** (or **Generic** if non-humanoid). Click *Apply*.
     * **Materials Tab:** Extract or assign your URP materials.
2. **Replace the Prototype Mesh:**
   * In `DeadEnd.unity`, expand the `Player` GameObject.
   * Expand the `Visual` child GameObject.
   * Delete or disable the prototype primitives (`Mannequin_Body`, `Visor_DirectionMarker`, `WeaponSocket`).
   * Drag your imported 3D character prefab as a child under `Visual`.
3. **Connect the Animator:**
   * Ensure your 3D model's `Animator` component is assigned the `Player3D.controller` (or your custom controller).
   * Ensure `PlayerController` has its `visualRoot` field pointing to `Visual` (or your model's root).
4. **Weapon Attachment (Optional):**
   * If your character wields a sledgehammer or weapon, locate the Right Hand bone in the 3D rig's hierarchy (`.../Hand.R`).
   * Create an empty child GameObject named `WeaponSocket` under the hand bone and place your 3D weapon mesh inside it.
   * Drag `WeaponSocket` into the `Weapon Socket` field on `PlayerController`.

---

## 6. Rendering & Shaders (Universal Render Pipeline)

* The project currently uses URP with **`Renderer2D.asset`**.
* 3D meshes rendered under the 2D Renderer will render cleanly using:
  * Materials with **`Universal Render Pipeline/2D/Sprite-Lit-Default`** (receives 2D lights).
  * Materials with **`Universal Render Pipeline/Unlit`** (constant ambient visibility).
  * Custom Cel/Toon shaders designed for URP.
* If you decide to transition the entire scene to full 3D lighting (real-time 3D directional lights and shadows):
  * Add a `UniversalRendererData` (Forward+ Renderer) to the `UniversalRP.asset` renderer list, or add a 3D Directional Light to the scene.

---

## 7. Change Summary & Commit History

| Commit | Summary |
|---|---|
| `fffea76` | `rework checkpoint` (clean baseline on `main`) |
| `9ddc601` | `feat(player): add 2.5D 3D character support, yaw rotation, ground check, and editor setup wizard` |
