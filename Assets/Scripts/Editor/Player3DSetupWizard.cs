using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Player3DSetupWizard
{
    private const string ControllerPath = "Assets/Animation/Player3D.controller";
    private const string MaterialPath = "Assets/Materials/Player3D_PrototypeMat.mat";

    [MenuItem("Tools/Alice in Helheim/Apply Blender Character to Player (With Textures)", false, 0)]
    public static void ApplyBlenderCharacter()
    {
        PlayerController playerController = Object.FindAnyObjectByType<PlayerController>();
        if (playerController == null)
        {
            EditorUtility.DisplayDialog("Apply Blender Character", "No PlayerController found in the active scene!", "OK");
            return;
        }

        GameObject playerGO = playerController.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(playerGO, "Apply Blender Character");

        // 1. Locate or create child 'Visual'
        Transform visualTransform = playerGO.transform.Find("Visual");
        if (visualTransform == null)
        {
            GameObject visualGO = new GameObject("Visual");
            visualGO.transform.SetParent(playerGO.transform, false);
            visualGO.transform.localPosition = Vector3.zero;
            visualGO.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            visualGO.transform.localScale = Vector3.one;
            visualTransform = visualGO.transform;
            Undo.RegisterCreatedObjectUndo(visualGO, "Create Visual Child");
        }
        visualTransform.gameObject.SetActive(true);

        // 2. Remove prototype mannequin primitives if present
        for (int i = visualTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = visualTransform.GetChild(i);
            if (child.name.StartsWith("Mannequin") || child.name.StartsWith("Visor") || child.name.StartsWith("Hammer") || child.name == "WeaponSocket")
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        // 3. Load Character model (prefer FBX, fall back to blend)
        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Character.fbx");
        if (modelPrefab == null)
        {
            modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Original.blend");
        }

        if (modelPrefab == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find Character.fbx or Original.blend in Assets/Character/!", "OK");
            return;
        }

        // 4. Instantiate character model if not already instantiated
        Transform existingModel = visualTransform.Find(modelPrefab.name);
        GameObject instance = existingModel != null ? existingModel.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, visualTransform);
        if (existingModel == null)
        {
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Character Model");
        }

        // 5. Load Extracted Materials
        Material bodyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Character/Materials/Character_Body_Mat.mat");
        Material eyesMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Character/Materials/Character_Eyes_Mat.mat");

        // 6. Assign Materials to Renderers
        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r.gameObject.name.Contains("Body") || r.gameObject.name == "Character_Body")
            {
                if (bodyMat != null) r.sharedMaterial = bodyMat;
            }
            else if (r.gameObject.name.Contains("Eyes") || r.gameObject.name == "Character_Eyes")
            {
                if (eyesMat != null) r.sharedMaterial = eyesMat;
            }
            else
            {
                if (bodyMat != null && (r.sharedMaterial == null || r.sharedMaterial.name.StartsWith("Default")))
                    r.sharedMaterial = bodyMat;
            }
            EditorUtility.SetDirty(r);
        }

        // 7. Connect Animator Controller
        AnimatorController animController = EnsureAnimatorController();
        Animator animator = instance.GetComponent<Animator>();
        if (animator == null)
        {
            animator = visualTransform.GetComponent<Animator>();
        }
        if (animator != null && animController != null && animator.runtimeAnimatorController == null)
        {
            animator.runtimeAnimatorController = animController;
            EditorUtility.SetDirty(animator);
        }

        // 8. Connect to PlayerController & disable 2D SpriteRenderer
        playerController.SetVisualRoot(visualTransform);
        visualTransform.localPosition = new Vector3(0f, -0.022f, 0f);
        visualTransform.localRotation = Quaternion.Euler(0f, 105f, 0f);
        playerGO.transform.rotation = Quaternion.identity;

        CapsuleCollider2D capsule = playerGO.GetComponent<CapsuleCollider2D>();
        if (capsule != null)
        {
            capsule.offset = new Vector2(0f, 0.42f);
            capsule.size = new Vector2(0.22f, 0.88f);
            EditorUtility.SetDirty(capsule);
        }

        SpriteRenderer sr = playerGO.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.enabled = false;
            EditorUtility.SetDirty(sr);
        }

        Animator rootAnimator = playerGO.GetComponent<Animator>();
        if (rootAnimator != null && rootAnimator != animator)
        {
            rootAnimator.enabled = false;
            EditorUtility.SetDirty(rootAnimator);
        }

        EditorUtility.SetDirty(playerGO);
        EditorSceneManager.MarkSceneDirty(playerGO.scene);

        EditorUtility.DisplayDialog(
            "Blender Character Applied",
            "Blender Character successfully configured with Textures & Materials!\n\n" +
            "• Extracted & mapped 'Character_Body_Mat' (paint_basecolor.png)\n" +
            "• Extracted & mapped 'Character_Eyes_Mat' (tripo_image_...jpg)\n" +
            "• Linked to PlayerController 2.5D visual root\n" +
            "• Connected 3D Animator controller\n" +
            "• 2D SpriteRenderer disabled non-destructively",
            "Awesome!"
        );
    }

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
            visualGO.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
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

        visualTransform.localPosition = new Vector3(0f, -0.022f, 0f);
        playerGO.transform.rotation = Quaternion.identity;

        CapsuleCollider2D capsule = playerGO.GetComponent<CapsuleCollider2D>();
        if (capsule != null)
        {
            capsule.offset = new Vector2(0f, 0.42f);
            capsule.size = new Vector2(0.22f, 0.88f);
            EditorUtility.SetDirty(capsule);
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
        return BuildFullAnimatorController();
    }

    [MenuItem("Tools/Alice in Helheim/Rebuild Full 3D Animator Controller (Combos & Jumps)", false, 4)]
    public static void ForceRebuildAnimatorController()
    {
        BuildFullAnimatorController();
        EditorUtility.DisplayDialog("Animator Controller", "3D Animator Controller rebuilt successfully with in-place combat, locomotion, running jump, and stationary jump!", "OK");
    }

    public static void BuildFullAnimatorControllerBatch()
    {
        BuildFullAnimatorController();
    }

    public static AnimatorController BuildFullAnimatorController()
    {
        string directory = Path.GetDirectoryName(ControllerPath);
        if (!Directory.Exists(directory) && directory != null)
        {
            Directory.CreateDirectory(directory);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // Core Parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsFalling", AnimatorControllerParameterType.Bool);
        controller.AddParameter("InCombat", AnimatorControllerParameterType.Bool);
        controller.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
        controller.AddParameter("AttackTrigger", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("JumpTrigger", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsRunningJump", AnimatorControllerParameterType.Bool);

        // Backward compatibility parameters
        controller.AddParameter("Motion", AnimatorControllerParameterType.Float);
        controller.AddParameter("Facing", AnimatorControllerParameterType.Float);
        controller.AddParameter("isRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isJumping", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("falling", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;

        // Load animation clips (prefer in-place variants to prevent root translation drift)
        AnimationClip idleClip = LoadClip("Assets/Animation/Idle.fbx");
        AnimationClip runClip = LoadClip("Assets/Animation/Run_InPlace.anim") ?? LoadClip("Assets/Animation/Running.fbx");
        AnimationClip fightIdleClip = LoadClip("Assets/Animation/Combat/Fighting_Idle_InPlace.anim") ?? LoadClip("Assets/Animation/Combat/Fighting Idle.fbx");
        AnimationClip boxing1Clip = LoadClip("Assets/Animation/Combat/Boxing_1_InPlace.anim") ?? LoadClip("Assets/Animation/Combat/Boxing 1.fbx");
        AnimationClip boxing2Clip = LoadClip("Assets/Animation/Combat/Boxing_2_InPlace.anim") ?? LoadClip("Assets/Animation/Combat/Boxing 2.fbx");
        AnimationClip kickClip = LoadClip("Assets/Animation/Combat/Kick_InPlace.anim") ?? LoadClip("Assets/Animation/Combat/Kick.fbx");
        AnimationClip runKickClip = LoadClip("Assets/Animation/Combat/Run_to_kick_InPlace.anim") ?? LoadClip("Assets/Animation/Combat/Run to kick.fbx");
        AnimationClip jumpClip = LoadClip("Assets/Animation/Jumping_InPlace.anim") ?? LoadClip("Assets/Animation/Jumping.fbx");
        AnimationClip runJumpClip = LoadClip("Assets/Animation/Running_Jump_InPlace.anim") ?? LoadClip("Assets/Animation/Running Jump.fbx");

        // Create 9 States (In-place combat combos, stationary jump, and running jump)
        var sIdle = sm.AddState("Idle"); sIdle.motion = idleClip;
        var sRun = sm.AddState("Run"); sRun.motion = runClip;
        var sFightIdle = sm.AddState("Fighting Idle"); sFightIdle.motion = fightIdleClip;
        var sBoxing1 = sm.AddState("Boxing 1"); sBoxing1.motion = boxing1Clip;
        var sBoxing2 = sm.AddState("Boxing 2"); sBoxing2.motion = boxing2Clip;
        var sKick = sm.AddState("Kick"); sKick.motion = kickClip;
        var sRunKick = sm.AddState("Run to kick"); sRunKick.motion = runKickClip;
        var sJump = sm.AddState("Jump"); sJump.motion = jumpClip;
        var sRunJump = sm.AddState("Running Jump"); sRunJump.motion = runJumpClip;

        sm.defaultState = sIdle;

        // 1. Idle transitions
        var toRunFromIdle = sIdle.AddTransition(sRun);
        toRunFromIdle.hasExitTime = false;
        toRunFromIdle.duration = 0.15f;
        toRunFromIdle.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var toFightFromIdle = sIdle.AddTransition(sFightIdle);
        toFightFromIdle.hasExitTime = false;
        toFightFromIdle.duration = 0.2f;
        toFightFromIdle.AddCondition(AnimatorConditionMode.If, 0, "InCombat");

        var toBox1FromIdle = sIdle.AddTransition(sBoxing1);
        toBox1FromIdle.hasExitTime = false;
        toBox1FromIdle.duration = 0.08f;
        toBox1FromIdle.AddCondition(AnimatorConditionMode.If, 0, "AttackTrigger");
        toBox1FromIdle.AddCondition(AnimatorConditionMode.Equals, 0, "AttackIndex");

        var toRunKickFromIdle = sIdle.AddTransition(sRunKick);
        toRunKickFromIdle.hasExitTime = false;
        toRunKickFromIdle.duration = 0.08f;
        toRunKickFromIdle.AddCondition(AnimatorConditionMode.If, 0, "AttackTrigger");
        toRunKickFromIdle.AddCondition(AnimatorConditionMode.Equals, 3, "AttackIndex");

        // 2. Run transitions
        var toIdleFromRun = sRun.AddTransition(sIdle);
        toIdleFromRun.hasExitTime = false;
        toIdleFromRun.duration = 0.15f;
        toIdleFromRun.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        toIdleFromRun.AddCondition(AnimatorConditionMode.IfNot, 0, "InCombat");

        var toFightFromRun = sRun.AddTransition(sFightIdle);
        toFightFromRun.hasExitTime = false;
        toFightFromRun.duration = 0.15f;
        toFightFromRun.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        toFightFromRun.AddCondition(AnimatorConditionMode.If, 0, "InCombat");

        var toBox1FromRun = sRun.AddTransition(sBoxing1);
        toBox1FromRun.hasExitTime = false;
        toBox1FromRun.duration = 0.08f;
        toBox1FromRun.AddCondition(AnimatorConditionMode.If, 0, "AttackTrigger");
        toBox1FromRun.AddCondition(AnimatorConditionMode.Equals, 0, "AttackIndex");

        var toRunKickFromRun = sRun.AddTransition(sRunKick);
        toRunKickFromRun.hasExitTime = false;
        toRunKickFromRun.duration = 0.08f;
        toRunKickFromRun.AddCondition(AnimatorConditionMode.If, 0, "AttackTrigger");
        toRunKickFromRun.AddCondition(AnimatorConditionMode.Equals, 3, "AttackIndex");

        // 3. Fighting Idle transitions
        var toIdleFromFight = sFightIdle.AddTransition(sIdle);
        toIdleFromFight.hasExitTime = false;
        toIdleFromFight.duration = 0.35f;
        toIdleFromFight.AddCondition(AnimatorConditionMode.IfNot, 0, "InCombat");
        toIdleFromFight.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        var toRunFromFight = sFightIdle.AddTransition(sRun);
        toRunFromFight.hasExitTime = false;
        toRunFromFight.duration = 0.15f;
        toRunFromFight.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var toBox1FromFight = sFightIdle.AddTransition(sBoxing1);
        toBox1FromFight.hasExitTime = false;
        toBox1FromFight.duration = 0.08f;
        toBox1FromFight.AddCondition(AnimatorConditionMode.If, 0, "AttackTrigger");
        toBox1FromFight.AddCondition(AnimatorConditionMode.Equals, 0, "AttackIndex");

        var toRunKickFromFight = sFightIdle.AddTransition(sRunKick);
        toRunKickFromFight.hasExitTime = false;
        toRunKickFromFight.duration = 0.08f;
        toRunKickFromFight.AddCondition(AnimatorConditionMode.If, 0, "AttackTrigger");
        toRunKickFromFight.AddCondition(AnimatorConditionMode.Equals, 3, "AttackIndex");

        // 4. Combat combo transitions (In-Place animations)
        var toBox2 = sBoxing1.AddTransition(sBoxing2);
        toBox2.hasExitTime = false;
        toBox2.duration = 0.08f;
        toBox2.AddCondition(AnimatorConditionMode.If, 0, "AttackTrigger");
        toBox2.AddCondition(AnimatorConditionMode.Equals, 1, "AttackIndex");

        var box1ToFight = sBoxing1.AddTransition(sFightIdle);
        box1ToFight.hasExitTime = true;
        box1ToFight.exitTime = 0.82f;
        box1ToFight.duration = 0.15f;
        box1ToFight.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        var box1ToRun = sBoxing1.AddTransition(sRun);
        box1ToRun.hasExitTime = true;
        box1ToRun.exitTime = 0.82f;
        box1ToRun.duration = 0.15f;
        box1ToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var toKick = sBoxing2.AddTransition(sKick);
        toKick.hasExitTime = false;
        toKick.duration = 0.08f;
        toKick.AddCondition(AnimatorConditionMode.If, 0, "AttackTrigger");
        toKick.AddCondition(AnimatorConditionMode.Equals, 2, "AttackIndex");

        var box2ToFight = sBoxing2.AddTransition(sFightIdle);
        box2ToFight.hasExitTime = true;
        box2ToFight.exitTime = 0.82f;
        box2ToFight.duration = 0.15f;
        box2ToFight.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        var box2ToRun = sBoxing2.AddTransition(sRun);
        box2ToRun.hasExitTime = true;
        box2ToRun.exitTime = 0.82f;
        box2ToRun.duration = 0.15f;
        box2ToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var kickToFight = sKick.AddTransition(sFightIdle);
        kickToFight.hasExitTime = true;
        kickToFight.exitTime = 0.88f;
        kickToFight.duration = 0.15f;
        kickToFight.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        var kickToRun = sKick.AddTransition(sRun);
        kickToRun.hasExitTime = true;
        kickToRun.exitTime = 0.88f;
        kickToRun.duration = 0.15f;
        kickToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var runKickToFight = sRunKick.AddTransition(sFightIdle);
        runKickToFight.hasExitTime = true;
        runKickToFight.exitTime = 0.82f;
        runKickToFight.duration = 0.15f;
        runKickToFight.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        var runKickToRun = sRunKick.AddTransition(sRun);
        runKickToRun.hasExitTime = true;
        runKickToRun.exitTime = 0.82f;
        runKickToRun.duration = 0.15f;
        runKickToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        // 5. Jump transitions (AnyState -> Jump or Running Jump on spacebar)
        var anyToJump = sm.AddAnyStateTransition(sJump);
        anyToJump.hasExitTime = false;
        anyToJump.duration = 0.05f;
        anyToJump.canTransitionToSelf = false;
        anyToJump.AddCondition(AnimatorConditionMode.If, 0, "JumpTrigger");
        anyToJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunningJump");

        var anyToRunJump = sm.AddAnyStateTransition(sRunJump);
        anyToRunJump.hasExitTime = false;
        anyToRunJump.duration = 0.05f;
        anyToRunJump.canTransitionToSelf = false;
        anyToRunJump.AddCondition(AnimatorConditionMode.If, 0, "JumpTrigger");
        anyToRunJump.AddCondition(AnimatorConditionMode.If, 0, "IsRunningJump");

        // 6. Jump landing transitions (for both Jump and Running Jump)
        foreach (var jState in new[] { sJump, sRunJump })
        {
            var jToRun = jState.AddTransition(sRun);
            jToRun.hasExitTime = false;
            jToRun.duration = 0.12f;
            jToRun.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
            jToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var jToFight = jState.AddTransition(sFightIdle);
            jToFight.hasExitTime = false;
            jToFight.duration = 0.15f;
            jToFight.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
            jToFight.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            jToFight.AddCondition(AnimatorConditionMode.If, 0, "InCombat");

            var jToIdle = jState.AddTransition(sIdle);
            jToIdle.hasExitTime = false;
            jToIdle.duration = 0.15f;
            jToIdle.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
            jToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            jToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "InCombat");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AliceinHelheim] Generated 3D Animator Controller at {ControllerPath}");

        return controller;
    }

    private static AnimationClip LoadClip(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        if (path.EndsWith(".anim"))
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

        var objects = AssetDatabase.LoadAllAssetsAtPath(path);
        if (objects != null)
        {
            foreach (var obj in objects)
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
        }
        return null;
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
