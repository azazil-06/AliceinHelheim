using UnityEngine;

/// <summary>
/// Humanoid foot grounding for the 2.5D 3D-character player.
///
/// The Mixamo clips retargeted onto the character put the soles anywhere from ~0.1 below to ~0.13 above the
/// 2D ground depending on the clip and frame, and the Rigidbody2D/CapsuleCollider2D knows nothing about the
/// feet. This component probes the 2D terrain (Physics2D) under each foot and uses Mecanim foot IK to
/// plant the soles on the ground. Feet that are clearly in the air (swing phase, jumps, kicks) are left
/// to the animation via a height-based IK weight.
///
/// Setup: lives on the same GameObject as the humanoid Animator (Player/Visual/Character) and needs
/// "IK Pass" enabled on layer 0 of Player3D.controller (the Player3DSetupWizard does both).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class PlayerFootIK : MonoBehaviour
{
    [Header("Ground Probe (2D)")]
    [Tooltip("Terrain layer(s) to plant the feet on (Layer 14 'base').")]
    [SerializeField] private LayerMask groundLayer = 1 << 14;
    [Tooltip("World units above the animated ankle where the downward ray starts.")]
    [SerializeField] private float probeStartHeight = 0.6f;
    [Tooltip("How far the ray travels down from its start (world units).")]
    [SerializeField] private float probeLength = 1.8f;

    [Header("Planting")]
    [Tooltip("Animated sole at or below this height above the ground is fully planted (IK weight 1).")]
    [SerializeField] private float fullPlantHeight = 0.06f;
    [Tooltip("Animated sole above this height is left to the animation (IK weight 0): swing phase, kicks, jumps.")]
    [SerializeField] private float releaseHeight = 0.28f;
    [Tooltip("How quickly the IK weight follows changes (higher = snappier).")]
    [SerializeField] private float weightSmoothing = 18f;

    [Header("Slope")]
    [SerializeField] private bool alignFootToSlope = true;
    [Range(0f, 45f)]
    [SerializeField] private float maxFootTilt = 20f;

    [Header("Body")]
    [Tooltip("Lower the body when a planted foot has to reach further down than the animation (slopes, ledges).")]
    [SerializeField] private bool lowerBodyForReach = true;
    [SerializeField] private float maxBodyDrop = 0.2f;
    [SerializeField] private float bodySmoothing = 14f;

    private Animator animator;
    private ContactFilter2D filter;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[6];
    private readonly float[] weights = new float[2];
    private float bodyOffset;
    [HideInInspector] public int debugCalls; [HideInInspector] public int debugLastLayer = -1;

    private static readonly AvatarIKGoal[] Goals = { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot };
    private static readonly HumanBodyBones[] Bones = { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };

    private struct Foot
    {
        public bool hit;
        public Vector3 animPos;
        public float groundY;
        public Vector2 normal;
        public float ankleHeight;
        public float soleHeight; // animated sole height above the ground
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        BuildFilter();
    }

    private void OnValidate()
    {
        fullPlantHeight = Mathf.Max(0f, fullPlantHeight);
        releaseHeight = Mathf.Max(fullPlantHeight + 0.01f, releaseHeight);
        BuildFilter();
    }

    private void BuildFilter()
    {
        filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(groundLayer);
        // Walkable surfaces only (ignore vertical walls), same range PlayerController uses for its ground check.
        filter.SetNormalAngle(35f, 145f);
    }

    private void OnDisable()
    {
        weights[0] = weights[1] = 0f;
        bodyOffset = 0f;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        debugCalls++; debugLastLayer = layerIndex;
        if (layerIndex != 0 || animator == null || !animator.isHuman) return;

        float dt = Time.deltaTime;
        float scale = transform.lossyScale.y;
        var feet = new Foot[2];
        float bodyTarget = 0f;

        for (int i = 0; i < 2; i++)
        {
            Transform bone = animator.GetBoneTransform(Bones[i]);
            if (bone == null) continue;

            Foot f = new Foot
            {
                animPos = bone.position,
                ankleHeight = (i == 0 ? animator.leftFeetBottomHeight : animator.rightFeetBottomHeight) * scale
            };

            Vector2 origin = new Vector2(f.animPos.x, f.animPos.y + probeStartHeight);
            int count = Physics2D.Raycast(origin, Vector2.down, filter, hits, probeLength);
            if (count > 0 && hits[0].collider != null)
            {
                f.hit = true;
                f.groundY = hits[0].point.y;
                f.normal = hits[0].normal;
                f.soleHeight = (f.animPos.y - f.ankleHeight) - f.groundY;
            }

            float target = 0f;
            if (f.hit)
            {
                float t = 1f - Mathf.InverseLerp(fullPlantHeight, releaseHeight, f.soleHeight);
                target = t * t * (3f - 2f * t); // smoothstep
            }
            weights[i] = Mathf.Lerp(weights[i], target, 1f - Mathf.Exp(-weightSmoothing * dt));

            if (f.hit && lowerBodyForReach)
            {
                float reach = (f.groundY + f.ankleHeight) - f.animPos.y; // < 0: foot must go down
                if (reach < 0f) bodyTarget = Mathf.Min(bodyTarget, reach * weights[i]);
            }

            feet[i] = f;
        }

        bodyTarget = Mathf.Clamp(bodyTarget, -maxBodyDrop, 0f);
        bodyOffset = Mathf.Lerp(bodyOffset, bodyTarget, 1f - Mathf.Exp(-bodySmoothing * dt));
        if (lowerBodyForReach && bodyOffset < -0.0005f)
        {
            animator.bodyPosition = animator.bodyPosition + Vector3.up * bodyOffset;
        }

        for (int i = 0; i < 2; i++)
        {
            Foot f = feet[i];
            AvatarIKGoal goal = Goals[i];

            if (!f.hit || weights[i] < 0.001f)
            {
                animator.SetIKPositionWeight(goal, 0f);
                animator.SetIKRotationWeight(goal, 0f);
                continue;
            }

            Vector3 pos = new Vector3(f.animPos.x, f.groundY + f.ankleHeight, f.animPos.z);
            animator.SetIKPositionWeight(goal, weights[i]);
            animator.SetIKPosition(goal, pos);

            if (alignFootToSlope)
            {
                Vector3 n = new Vector3(f.normal.x, f.normal.y, 0f);
                Quaternion tilt = Quaternion.FromToRotation(Vector3.up, n);
                tilt = Quaternion.RotateTowards(Quaternion.identity, tilt, maxFootTilt);
                animator.SetIKRotationWeight(goal, weights[i]);
                animator.SetIKRotation(goal, tilt * animator.GetIKRotation(goal));
            }
            else
            {
                animator.SetIKRotationWeight(goal, 0f);
            }
        }
    }
}
