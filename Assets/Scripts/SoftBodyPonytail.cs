using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural Soft-Body Ponytail & Hair Physics for 2.5D sidescroller character.
/// Simulates secondary dynamic motion, drag, bounce, gravity, and inertial whip
/// across multi-segment hair bone chains using Verlet integration and distance constraints.
/// </summary>
[DefaultExecutionOrder(100)] // Runs after Mecanim Animator poses bones
public class SoftBodyPonytail : MonoBehaviour
{
    [Serializable]
    public class HairNode
    {
        public Transform bone;
        public Transform child;
        [HideInInspector] public Quaternion initialLocalRotation;
        [HideInInspector] public Vector3 initialLocalPosition;
        [HideInInspector] public Vector3 childLocalPosition;
        [HideInInspector] public float boneLength;
        [HideInInspector] public Vector3 currentPosition;
        [HideInInspector] public Vector3 previousPosition;
    }

    [Serializable]
    public class HairChain
    {
        public string chainName = "Hair Chain";
        public Transform rootBone;
        public HairNode[] nodes;
    }

    [Header("Physics Tuning")]
    [Tooltip("Spring elasticity pulling hair back to its rest/animated shape (0.1 = soft silk, 0.8 = stiff).")]
    [Range(0.05f, 1f)]
    public float stiffness = 0.38f;

    [Tooltip("Damping / air friction to calm oscillations (0 = forever oscillating, 0.9 = overdamped).")]
    [Range(0.05f, 0.95f)]
    public float damping = 0.28f;

    [Tooltip("Downward gravity force pulling the ponytail down.")]
    public float gravity = 3.5f;

    [Tooltip("Aerodynamic drag lagging hair behind character running velocity.")]
    [Range(0f, 2f)]
    public float movementLag = 0.45f;

    [Tooltip("Vertical buoyancy lifting hair when jumping or falling.")]
    [Range(0f, 2f)]
    public float jumpFloat = 0.8f;

    [Tooltip("Maximum deflection angle (in degrees) per bone segment from its rest direction.")]
    [Range(20f, 90f)]
    public float maxAngle = 75f;

    [Header("Head Collision")]
    [Tooltip("Prevents hair from penetrating the skull / upper neck.")]
    public bool enableHeadCollision = true;
    public Vector3 headColliderOffset = new Vector3(0f, -0.05f, 0f);
    public float headColliderRadius = 0.22f;

    [Header("Sidescroller 180° Turn Whip")]
    [Tooltip("Whips hair smoothly around to the new back side when character snaps facing Left/Right.")]
    public bool flipCompensation = true;

    [Header("Idle Subtle Sway")]
    [Tooltip("Gentle natural breathing / wind jiggle when character is stationary.")]
    public bool idleSway = true;
    public float swayFrequency = 1.6f;
    public float swayAmplitude = 0.012f;

    [Header("Chains & References")]
    public Transform headTransform;
    public Transform visualRoot;
    public Rigidbody2D rootRigidbody;
    public List<HairChain> hairChains = new List<HairChain>();

    // Runtime state
    private Quaternion prevVisualRotation;
    private Vector2 prevRbVelocity;
    private bool isInitialized = false;

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        if (!isInitialized)
        {
            Initialize();
        }
    }

    /// <summary>
    /// Discovers and binds hair bones from the hierarchy if not already populated.
    /// </summary>
    public void Initialize()
    {
        if (headTransform == null)
        {
            headTransform = FindTransformRecursive(transform, "Head");
            if (headTransform == null && transform.parent != null)
            {
                headTransform = FindTransformRecursive(transform.parent, "Head");
            }
        }

        if (visualRoot == null)
        {
            Transform t = transform;
            while (t != null)
            {
                if (t.name == "Visual")
                {
                    visualRoot = t;
                    break;
                }
                t = t.parent;
            }
        }

        if (rootRigidbody == null)
        {
            rootRigidbody = GetComponentInParent<Rigidbody2D>();
        }

        // Auto-discover Hair_A and Hair_B if chains are unassigned
        if (hairChains == null || hairChains.Count == 0)
        {
            hairChains = new List<HairChain>();
            if (headTransform != null)
            {
                Transform chainARoot = FindTransformRecursive(headTransform, "Hair_A_01");
                if (chainARoot != null)
                {
                    hairChains.Add(BuildChain("Hair_A (Upper Ponytail)", chainARoot));
                }

                Transform chainBRoot = FindTransformRecursive(headTransform, "Hair_B_01");
                if (chainBRoot != null)
                {
                    hairChains.Add(BuildChain("Hair_B (Lower Ponytail)", chainBRoot));
                }
            }
        }
        else
        {
            // Re-cache node baseline positions for existing chains
            foreach (var chain in hairChains)
            {
                if (chain != null && chain.nodes != null)
                {
                    foreach (var node in chain.nodes)
                    {
                        CacheNodeData(node);
                    }
                }
            }
        }

        if (visualRoot != null)
        {
            prevVisualRotation = visualRoot.rotation;
        }

        isInitialized = true;
    }

    private HairChain BuildChain(string name, Transform root)
    {
        HairChain chain = new HairChain { chainName = name, rootBone = root };
        List<HairNode> nodeList = new List<HairNode>();

        Transform curr = root;
        while (curr != null && curr.childCount > 0)
        {
            Transform child = curr.GetChild(0);
            HairNode node = new HairNode
            {
                bone = curr,
                child = child
            };
            CacheNodeData(node);
            nodeList.Add(node);

            // Stop if child is the end marker (_end)
            if (child.name.EndsWith("_end") || child.childCount == 0)
            {
                break;
            }
            curr = child;
        }

        chain.nodes = nodeList.ToArray();
        return chain;
    }

    private void CacheNodeData(HairNode node)
    {
        if (node == null || node.bone == null || node.child == null) return;

        node.initialLocalRotation = node.bone.localRotation;
        node.initialLocalPosition = node.bone.localPosition;
        node.childLocalPosition = node.child.localPosition;
        node.boneLength = Vector3.Distance(node.bone.position, node.child.position);
        node.currentPosition = node.child.position;
        node.previousPosition = node.child.position;
    }

    private void LateUpdate()
    {
        if (!isInitialized || hairChains == null || hairChains.Count == 0) return;

        float dt = Time.deltaTime;
        if (dt <= 0f || dt > 0.1f) dt = 0.016f; // Clamp delta time against hitches

        HandleTurnFlip();

        // Sample velocity & movement forces
        Vector3 playerVel = Vector3.zero;
        if (rootRigidbody != null)
        {
            Vector2 rbVel = rootRigidbody.linearVelocity;
            playerVel = new Vector3(rbVel.x, rbVel.y, 0f);
            prevRbVelocity = rbVel;
        }

        Vector3 headCenter = headTransform != null
            ? headTransform.position + headTransform.TransformDirection(headColliderOffset)
            : transform.position;

        // Simulate each hair strand
        for (int c = 0; c < hairChains.Count; c++)
        {
            var chain = hairChains[c];
            if (chain == null || chain.nodes == null) continue;

            SimulateChain(chain, dt, playerVel, headCenter);
        }
    }

    private void HandleTurnFlip()
    {
        if (!flipCompensation || visualRoot == null) return;

        Quaternion currentVisualRot = visualRoot.rotation;
        float angleDiff = Quaternion.Angle(prevVisualRotation, currentVisualRot);

        // Snap flip detected (e.g., yaw 90° to 270°)
        if (angleDiff > 60f)
        {
            Quaternion flipRot = currentVisualRot * Quaternion.Inverse(prevVisualRotation);
            Vector3 pivot = headTransform != null ? headTransform.position : visualRoot.position;

            for (int c = 0; c < hairChains.Count; c++)
            {
                var chain = hairChains[c];
                if (chain?.nodes == null) continue;

                for (int i = 0; i < chain.nodes.Length; i++)
                {
                    var node = chain.nodes[i];
                    node.currentPosition = pivot + flipRot * (node.currentPosition - pivot);
                    node.previousPosition = pivot + flipRot * (node.previousPosition - pivot);
                }
            }
        }

        prevVisualRotation = currentVisualRot;
    }

    private void SimulateChain(HairChain chain, float dt, Vector3 playerVel, Vector3 headCenter)
    {
        // 1. Reset each bone to animated base pose first so Animator drives base hierarchy
        for (int i = 0; i < chain.nodes.Length; i++)
        {
            var node = chain.nodes[i];
            if (node.bone != null)
            {
                node.bone.localRotation = node.initialLocalRotation;
            }
        }

        // 2. Cascade Verlet integration down the bone chain
        for (int i = 0; i < chain.nodes.Length; i++)
        {
            var node = chain.nodes[i];
            if (node.bone == null || node.child == null) continue;

            Transform bone = node.bone;
            Vector3 pivot = bone.position;

            // Rest position in world space according to current head/bone orientation
            Vector3 restTipPos = bone.TransformPoint(node.childLocalPosition);
            Vector3 restDir = (restTipPos - pivot).normalized;

            // Velocity with damping
            Vector3 velocity = (node.currentPosition - node.previousPosition) * (1f - damping);

            // Restoring spring force towards animated rest shape
            Vector3 springForce = (restTipPos - node.currentPosition) * (stiffness * 60f * dt);

            // Gravity
            Vector3 gravityForce = Vector3.down * (gravity * dt);

            // Aerodynamic drag lagging behind player movement
            Vector3 windForce = -new Vector3(playerVel.x * movementLag, playerVel.y * jumpFloat, 0f) * dt;

            // Idle jiggle / natural breathing sway
            Vector3 swayForce = Vector3.zero;
            if (idleSway && playerVel.sqrMagnitude < 0.1f)
            {
                float t = Time.time * swayFrequency + i * 0.45f;
                swayForce = new Vector3(Mathf.Sin(t), Mathf.Cos(t * 0.7f), 0f) * swayAmplitude;
            }

            // Integrate next position
            Vector3 nextPos = node.currentPosition + velocity + springForce + gravityForce + windForce + swayForce;

            // Head collision constraint (spherical pushout)
            if (enableHeadCollision && headTransform != null)
            {
                Vector3 toNext = nextPos - headCenter;
                float dist = toNext.magnitude;
                if (dist < headColliderRadius && dist > 0.0001f)
                {
                    nextPos = headCenter + (toNext / dist) * headColliderRadius;
                }
            }

            // Distance constraint (rigid bone length)
            Vector3 fromPivot = nextPos - pivot;
            float distFromPivot = fromPivot.magnitude;
            Vector3 newDir;

            if (distFromPivot > 0.0001f)
            {
                newDir = fromPivot / distFromPivot;
            }
            else
            {
                newDir = restDir;
            }

            // Angle limit constraint (cone clamp from rest direction)
            float angle = Vector3.Angle(restDir, newDir);
            if (angle > maxAngle)
            {
                newDir = Vector3.RotateTowards(restDir, newDir, maxAngle * Mathf.Deg2Rad, 0f);
            }

            nextPos = pivot + newDir * node.boneLength;

            // Update particle state
            node.previousPosition = node.currentPosition;
            node.currentPosition = nextPos;

            // Rotate bone to point along new direction
            Quaternion deltaRot = Quaternion.FromToRotation(restDir, newDir);
            bone.rotation = deltaRot * bone.rotation;
        }
    }

    /// <summary>
    /// Applies an external impulse (e.g. from combat strike, kick, or landing shock).
    /// </summary>
    public void AddImpulse(Vector3 impulse)
    {
        if (hairChains == null) return;
        for (int c = 0; c < hairChains.Count; c++)
        {
            var chain = hairChains[c];
            if (chain?.nodes == null) continue;
            for (int i = 0; i < chain.nodes.Length; i++)
            {
                chain.nodes[i].previousPosition -= impulse * 0.016f;
            }
        }
    }

    private Transform FindTransformRecursive(Transform parent, string targetName)
    {
        if (parent == null) return null;
        if (parent.name == targetName) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindTransformRecursive(parent.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (hairChains == null) return;

        // Draw Head Collider
        if (enableHeadCollision && headTransform != null)
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.35f);
            Vector3 center = headTransform.position + headTransform.TransformDirection(headColliderOffset);
            Gizmos.DrawWireSphere(center, headColliderRadius);
        }

        // Draw Hair Chains
        for (int c = 0; c < hairChains.Count; c++)
        {
            var chain = hairChains[c];
            if (chain?.nodes == null) continue;

            Gizmos.color = c == 0 ? Color.cyan : Color.magenta;
            for (int i = 0; i < chain.nodes.Length; i++)
            {
                var node = chain.nodes[i];
                if (node?.bone != null)
                {
                    Vector3 tip = Application.isPlaying ? node.currentPosition : (node.child != null ? node.child.position : node.bone.position);
                    Gizmos.DrawLine(node.bone.position, tip);
                    Gizmos.DrawWireSphere(tip, 0.02f);
                }
            }
        }
    }
}
