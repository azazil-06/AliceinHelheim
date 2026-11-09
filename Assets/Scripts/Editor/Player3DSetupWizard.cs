using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Player3DSetupWizard
{
    private const string ControllerPath = "Assets/Animation/Player3D.controller";
    private const string MaterialPath = "Assets/Materials/Player3D_PrototypeMat.mat";

    [MenuItem("Tools/Alice in Helheim/Setup 3D Character Rig (Active Scene)", false, 1)]
    public static void Setup3DCharacterInScene()
    {
        PlayerController playerController = Object.FindAnyObjectByType<PlayerController>();
        if (playerController == null)
        {
            EditorUtility.DisplayDialog("Setup 3D Character", "No PlayerController found in the current scene!", "OK");
            return;
        }

        GameObject playerGO = playerController.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(playerGO, "Setup 3D Character Rig");

        // 1. Locate or create child 'Visual'
        Transform visualTransform = playerGO.transform.Find("Visual");
        if (visualTransform == null)
        {
            GameObject visualGO = new GameObject("Visual");
            visualGO.transform.SetParent(playerGO.transform, false);
            visualGO.transform.localPosition = Vector3.zero;
            visualGO.transform.localRotation = Quaternion.Euler(0f, 75f, 0f);
            visualGO.transform.localScale = Vector3.one;
            visualTransform = visualGO.transform;
            Undo.RegisterCreatedObjectUndo(visualGO, "Create Visual Child");
        }
        visualTransform.gameObject.SetActive(true);

        // 2. Ensure prototype 3D mesh if Visual is empty
        if (visualTransform.childCount == 0)
        {
            CreatePrototypeMesh(visualTransform);
        }

        // 3. Ensure 3D Animator Controller exists and is attached
        AnimatorController animController = EnsureAnimatorController();
        Animator animator = visualTransform.GetComponent<Animator>();
        if (animator == null)
        {
            animator = visualTransform.gameObject.AddComponent<Animator>();
        }
        if (animController != null && animator.runtimeAnimatorController == null)
        {
            animator.runtimeAnimatorController = animController;
        }

        // 4. Update PlayerController reference & disable 2D SpriteRenderer
        playerController.SetVisualRoot(visualTransform);

        SpriteRenderer sr = playerGO.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.enabled = false;
        }

        // 5. If root GameObject has an Animator, disable it so child Animator drives the 3D rig
        Animator rootAnimator = playerGO.GetComponent<Animator>();
        if (rootAnimator != null && rootAnimator != animator)
        {
            rootAnimator.enabled = false;
        }

        EditorUtility.SetDirty(playerGO);
        EditorSceneManager.MarkSceneDirty(playerGO.scene);

        EditorUtility.DisplayDialog(
            "Setup 3D Character",
            "3D Character Rig configured successfully!\n\n" +
            "• Child GameObject 'Visual' created/configured.\n" +
            "• 2.5D smooth yaw rotation & camera tilt linked.\n" +
            "• Prototype 3D mannequin attached.\n" +
            "• Player3D.controller linked to Animator.\n" +
            "• 2D SpriteRenderer disabled non-destructively.\n\n" +
            "You can replace the prototype mesh inside 'Visual' with your imported 3D model FBX whenever you are ready!",
            "Got it!"
        );
    }

    [MenuItem("Tools/Alice in Helheim/Toggle 2D Sprite vs 3D Model", false, 2)]
    public static void Toggle2DAnd3D()
    {
        PlayerController playerController = Object.FindAnyObjectByType<PlayerController>();
        if (playerController == null)
        {
            EditorUtility.DisplayDialog("Toggle Visual", "No PlayerController found in the scene!", "OK");
            return;
        }

        GameObject playerGO = playerController.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(playerGO, "Toggle 2D/3D Visual");

        Transform visualTransform = playerGO.transform.Find("Visual");
        SpriteRenderer sr = playerGO.GetComponent<SpriteRenderer>();
        Animator rootAnimator = playerGO.GetComponent<Animator>();

        if (visualTransform != null && visualTransform.gameObject.activeSelf)
        {
            // Switch to 2D
            visualTransform.gameObject.SetActive(false);
            if (sr != null) sr.enabled = true;
            if (rootAnimator != null) rootAnimator.enabled = true;
            playerController.SetVisualRoot(null);
            Debug.Log("[AliceinHelheim] Switched Player visual to: 2D SPRITE");
        }
        else
        {
            // Switch to 3D
            if (visualTransform != null) visualTransform.gameObject.SetActive(true);
            if (sr != null) sr.enabled = false;
            if (rootAnimator != null) rootAnimator.enabled = false;
            playerController.SetVisualRoot(visualTransform);
            Debug.Log("[AliceinHelheim] Switched Player visual to: 3D MODEL");
        }

        EditorUtility.SetDirty(playerGO);
        EditorSceneManager.MarkSceneDirty(playerGO.scene);
    }

    [MenuItem("Tools/Alice in Helheim/Generate 3D Animator Controller", false, 3)]
    public static AnimatorController EnsureAnimatorController()
    {
        if (File.Exists(ControllerPath))
        {
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        }

        string directory = Path.GetDirectoryName(ControllerPath);
        if (!Directory.Exists(directory) && directory != null)
        {
            Directory.CreateDirectory(directory);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // Parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsFalling", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        // Backward compatibility parameters
        controller.AddParameter("Motion", AnimatorControllerParameterType.Float);
        controller.AddParameter("Facing", AnimatorControllerParameterType.Float);
        controller.AddParameter("isRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isJumping", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("falling", AnimatorControllerParameterType.Bool);

        // Layers & States
        var rootStateMachine = controller.layers[0].stateMachine;

        var idleState = rootStateMachine.AddState("Idle");
        var runState = rootStateMachine.AddState("Run");
        var jumpState = rootStateMachine.AddState("Jump");
        var fallState = rootStateMachine.AddState("Fall");
        var attackState = rootStateMachine.AddState("Attack");

        rootStateMachine.defaultState = idleState;

        // Transitions: Idle <-> Run
        var toRun = idleState.AddTransition(runState);
        toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        toRun.hasExitTime = false;
        toRun.duration = 0.1f;

        var toIdle = runState.AddTransition(idleState);
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        toIdle.hasExitTime = false;
        toIdle.duration = 0.1f;

        // Transitions: AnyState -> Jump
        var anyToJump = rootStateMachine.AddAnyStateTransition(jumpState);
        anyToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
        anyToJump.hasExitTime = false;
        anyToJump.duration = 0.05f;

        // Jump -> Fall
        var jumpToFall = jumpState.AddTransition(fallState);
        jumpToFall.AddCondition(AnimatorConditionMode.If, 0, "IsFalling");
        jumpToFall.hasExitTime = false;
        jumpToFall.duration = 0.1f;

        // Transitions: AnyState -> Fall (walked off ledge)
        var anyToFall = rootStateMachine.AddAnyStateTransition(fallState);
        anyToFall.AddCondition(AnimatorConditionMode.If, 0, "IsFalling");
        anyToFall.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        anyToFall.hasExitTime = false;
        anyToFall.duration = 0.1f;

        // Fall -> Idle (landing)
        var fallToIdle = fallState.AddTransition(idleState);
        fallToIdle.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        fallToIdle.hasExitTime = false;
        fallToIdle.duration = 0.08f;

        // Transitions: AnyState -> Attack
        var anyToAttack = rootStateMachine.AddAnyStateTransition(attackState);
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        anyToAttack.hasExitTime = false;
        anyToAttack.duration = 0.04f;

        // Attack -> Idle
        var attackToIdle = attackState.AddTransition(idleState);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 0.9f;
        attackToIdle.duration = 0.1f;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AliceinHelheim] Created 3D Animator Controller at {ControllerPath}");

        return controller;
    }

    private static void CreatePrototypeMesh(Transform visualRoot)
    {
        Material mat = EnsurePrototypeMaterial();

        // 1. Mannequin Body
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Mannequin_Body";
        body.transform.SetParent(visualRoot, false);
        body.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        body.transform.localScale = new Vector3(0.5f, 0.65f, 0.5f);
        Object.DestroyImmediate(body.GetComponent<Collider>()); // 2.5D uses Rigidbody2D colliders
        if (mat != null) body.GetComponent<Renderer>().sharedMaterial = mat;

        // 2. Visor / Head indicator (shows facing direction in 3D)
        GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = "Visor_DirectionMarker";
        visor.transform.SetParent(visualRoot, false);
        visor.transform.localPosition = new Vector3(0f, 1.05f, 0.22f);
        visor.transform.localScale = new Vector3(0.35f, 0.12f, 0.15f);
        Object.DestroyImmediate(visor.GetComponent<Collider>());
        if (mat != null) visor.GetComponent<Renderer>().sharedMaterial = mat;

        // 3. Weapon Socket & Prototype Sledgehammer
        GameObject weaponSocket = new GameObject("WeaponSocket");
        weaponSocket.transform.SetParent(visualRoot, false);
        weaponSocket.transform.localPosition = new Vector3(0.35f, 0.5f, 0.2f);
        weaponSocket.transform.localRotation = Quaternion.Euler(20f, 10f, -15f);

        // Hammer Handle
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.name = "Hammer_Handle";
        handle.transform.SetParent(weaponSocket.transform, false);
        handle.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        handle.transform.localScale = new Vector3(0.05f, 0.45f, 0.05f);
        Object.DestroyImmediate(handle.GetComponent<Collider>());
        if (mat != null) handle.GetComponent<Renderer>().sharedMaterial = mat;

        // Hammer Head
        GameObject hammerHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hammerHead.name = "Hammer_Head";
        hammerHead.transform.SetParent(weaponSocket.transform, false);
        hammerHead.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        hammerHead.transform.localScale = new Vector3(0.22f, 0.22f, 0.45f);
        Object.DestroyImmediate(hammerHead.GetComponent<Collider>());
        if (mat != null) hammerHead.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static Material EnsurePrototypeMaterial()
    {
        if (File.Exists(MaterialPath))
        {
            return AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        }
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material mat = new Material(shader)
        {
            name = "Player3D_PrototypeMat",
            color = new Color(0.85f, 0.35f, 0.25f, 1f) // Crimson / Alice aesthetic
        };

        string dir = Path.GetDirectoryName(MaterialPath);
        if (!Directory.Exists(dir) && dir != null)
        {
            Directory.CreateDirectory(dir);
        }

        AssetDatabase.CreateAsset(mat, MaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
