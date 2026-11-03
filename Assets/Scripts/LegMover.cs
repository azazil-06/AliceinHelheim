using UnityEngine;

// Attach to each Leg (Leg1-Leg8) under BodyTarget.
// ikTarget = the *_IK_Target object (NOT the effector bone tip)
//
// Key improvements:
// 1. Root velocity prediction: feet step ahead of movement direction so they don't lag behind at high speeds.
// 2. Alternating tetrapod gait: Group 0 and Group 1 alternate so exactly 4 legs are always grounded.
// 3. Dynamic scuttle speed: scales up to rapid-fire frequencies during high-speed sprint bursts.
// 4. Attack override: allows SpyderBehaviour to drive front legs into visceral rearing and stabbing attacks.
// 5. Airborne / Jump handling: tucks legs naturally beneath the body in mid-air instead of stretching to the floor.
// 6. Scale-flip recovery: detects when the body turns around and re-anchors feet without criss-crossing.
// 7. SmoothStep arcing: eliminates harsh linear snapping at step start/end.
[DefaultExecutionOrder(-10)] // Run BEFORE IKManager2D (order 0) so solvers use current-frame targets
public class LegMover : MonoBehaviour
{
    [Header("References")]
    public Transform ikTarget;
    [Tooltip("Legacy reference kept for inspector compatibility; gaitGroup is now used.")]
    public LegMover opposingLeg;

    [Header("Gait")]
    [Tooltip("0 = Group A, 1 = Group B. Legs in opposite groups alternate stepping.")]
    [Range(0, 1)]
    public int gaitGroup = 0;

    [Header("Step Tuning")]
    [Tooltip("Horizontal distance from rest before a step triggers.")]
    public float stepDistance       = 0.35f;
    [Tooltip("Arc height of the foot while stepping.")]
    public float stepHeight         = 0.18f;
    [Tooltip("Base speed multiplier of the step arc. Dynamically accelerates during sprints.")]
    public float stepSpeed          = 9f;
    [Tooltip("How far ahead in the direction of movement the foot plants (1.0 = full duration lead).")]
    public float velocityPrediction = 1f;

    [Header("Airborne / Jump Handling")]
    [Tooltip("Height clearance above ground before legs tuck into an airborne jump pose.")]
    public float airborneThreshold  = 0.38f;
    [Tooltip("Upward tuck offset for the foot relative to its mount while airborne.")]
    public float airborneTuckHeight = 0.15f;
    [Tooltip("Read-only: true when the spider is airborne.")]
    public bool isAirborne;

    [Header("Attack Override")]
    [Tooltip("Set by SpyderBehaviour during attack so this leg does not lock to ground anchor.")]
    public bool isAttacking;

    [Header("Bone Stiffness")]
    [Tooltip("Snap speed to anchor while planted (0 = rigid lock).")]
    public float plantSnapSpeed     = 0f;

    [Header("Ground Detection")]
    public LayerMask groundLayer;
    public float groundRayLength    = 5f;

    [Header("Debug (read-only)")]
    public bool isStepping;

    // Static gait tracking: counts how many legs in each group are currently mid-step
    private static int s_group0Stepping = 0;
    private static int s_group1Stepping = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetGaitCounters()
    {
        s_group0Stepping = 0;
        s_group1Stepping = 0;
    }

    Vector3 _restLocalPos;     // Foot rest position in this transform's local space
    Vector3 _footAnchor;       // Planted foot world position
    Vector3 _stepFrom;         // World position where step started
    Vector3 _stepTarget;       // World position where step will land
    float   _t;                // Normalized step time (0..1)

    Vector3 _lastRootPos;
    Vector3 _smoothedVelocity;
    float   _lastScaleSign = 1f;
    bool    _inGaitCount = false;
    SpyderBehaviour _spyderBehaviour;

    void Start()
    {
        if (ikTarget == null)
        {
            Debug.LogError($"[LegMover] {gameObject.name}: ikTarget is not assigned!", this);
            enabled = false;
            return;
        }

        _restLocalPos      = transform.InverseTransformPoint(ikTarget.position);
        _footAnchor        = ikTarget.position;
        _stepFrom          = _footAnchor;
        _stepTarget        = _footAnchor;
        _lastRootPos       = transform.position;
        _lastScaleSign     = Mathf.Sign(transform.lossyScale.x);

        _spyderBehaviour   = GetComponentInParent<SpyderBehaviour>();
    }

    void OnDisable()
    {
        EndStep(false);
    }

    void LateUpdate()
    {
        if (ikTarget == null) return;

        float dt = Time.deltaTime;

        // ── 1. Attack Override ──────────────────────────────────
        // When SpyderBehaviour is driving this leg in an attack strike, do not override
        if (isAttacking)
        {
            if (isStepping) EndStep(false);
            return;
        }

        // ── 2. Track body velocity ──────────────────────────────
        Vector3 curPos = transform.position;
        if (dt > 0.0001f)
        {
            Vector3 rawVel = (curPos - _lastRootPos) / dt;
            _smoothedVelocity = Vector3.Lerp(_smoothedVelocity, rawVel, dt * 20f);
        }
        _lastRootPos = curPos;

        // ── 3. Detect scale flip (body turned around) ───────────
        float currentSign = Mathf.Sign(transform.lossyScale.x);
        if (_lastScaleSign != 0f && Mathf.Sign(_lastScaleSign) != currentSign)
        {
            _lastScaleSign = currentSign;
            Reanchor();
            return;
        }
        _lastScaleSign = currentSign;

        // ── 4. Check Airborne State (Jumping / In Air) ───────────
        bool currentlyAirborne = false;
        if (_spyderBehaviour != null && _spyderBehaviour.isAirborne)
        {
            currentlyAirborne = true;
        }
        else if (groundLayer.value != 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                new Vector2(transform.position.x, transform.position.y + 0.5f),
                Vector2.down,
                groundRayLength,
                groundLayer);

            if (hit.collider != null)
            {
                float clearance = transform.position.y - hit.point.y;
                if (clearance > airborneThreshold)
                    currentlyAirborne = true;
            }
            else
            {
                currentlyAirborne = true;
            }
        }

        if (currentlyAirborne)
        {
            isAirborne = true;
            if (isStepping)
            {
                EndStep(false);
            }

            // Smoothly tuck foot upward into a curved jumping pose
            Vector3 tuckPos = transform.TransformPoint(_restLocalPos);
            tuckPos.y += airborneTuckHeight;
            ikTarget.position = Vector3.Lerp(ikTarget.position, tuckPos, dt * 14f);
            _footAnchor = ikTarget.position;
            return;
        }
        else if (isAirborne)
        {
            // Landing touchdown: re-anchor to ground
            isAirborne = false;
            Reanchor();
        }

        // ── 5. Step Logic ───────────────────────────────────────
        if (!isStepping)
        {
            // Lock foot anchor
            if (plantSnapSpeed <= 0f)
                ikTarget.position = _footAnchor;
            else
                ikTarget.position = Vector3.MoveTowards(ikTarget.position, _footAnchor, plantSnapSpeed * dt);

            // Check if step needed
            Vector3 desired = GetRestPosition();
            float deltaX = Mathf.Abs(desired.x - _footAnchor.x);
            float deltaY = Mathf.Abs(desired.y - _footAnchor.y);

            bool needsStep = deltaX > stepDistance || deltaY > stepDistance * 1.5f;

            if (needsStep && CanGroupStep(gaitGroup))
            {
                StartStep(desired);
            }
        }
        else
        {
            // Dynamic speed scaling: scuttle faster during high-speed bursts
            float speedRatio = Mathf.Clamp01(_smoothedVelocity.magnitude / 7.2f);
            float currentStepSpeed = Mathf.Lerp(stepSpeed, stepSpeed * 1.8f, speedRatio);
            float currentStepHeight = Mathf.Lerp(stepHeight, stepHeight * 0.7f, speedRatio);

            _t += dt * currentStepSpeed;
            _t = Mathf.Clamp01(_t);

            float smoothT = Mathf.SmoothStep(0f, 1f, _t);
            Vector3 pos = Vector3.Lerp(_stepFrom, _stepTarget, smoothT);
            pos.y += Mathf.Sin(smoothT * Mathf.PI) * currentStepHeight;

            ikTarget.position = pos;

            if (_t >= 1f)
            {
                EndStep(true);
            }
        }
    }

    void StartStep(Vector3 baseTarget)
    {
        _stepFrom   = _footAnchor;
        _stepTarget = PredictStepTarget(baseTarget);
        _t          = 0f;
        isStepping  = true;

        if (!_inGaitCount)
        {
            _inGaitCount = true;
            if (gaitGroup == 0) s_group0Stepping++;
            else s_group1Stepping++;
        }
    }

    void EndStep(bool updateAnchor)
    {
        if (updateAnchor)
        {
            _footAnchor = _stepTarget;
            if (ikTarget != null)
                ikTarget.position = _footAnchor;
        }

        isStepping = false;

        if (_inGaitCount)
        {
            _inGaitCount = false;
            if (gaitGroup == 0) s_group0Stepping = Mathf.Max(0, s_group0Stepping - 1);
            else s_group1Stepping = Mathf.Max(0, s_group1Stepping - 1);
        }
    }

    public void Reanchor()
    {
        isAttacking = false;
        EndStep(false);
        _footAnchor = GetRestPosition();
        _stepFrom   = _footAnchor;
        _stepTarget = _footAnchor;
        if (ikTarget != null)
            ikTarget.position = _footAnchor;
    }

    static bool CanGroupStep(int group)
    {
        return group == 0 ? (s_group1Stepping == 0) : (s_group0Stepping == 0);
    }

    Vector3 PredictStepTarget(Vector3 baseTarget)
    {
        if (velocityPrediction > 0f)
        {
            float speedRatio = Mathf.Clamp01(_smoothedVelocity.magnitude / 7.2f);
            float currentStepSpeed = Mathf.Lerp(stepSpeed, stepSpeed * 1.8f, speedRatio);
            float stepDuration = 1f / Mathf.Max(currentStepSpeed, 0.01f);
            baseTarget.x += _smoothedVelocity.x * stepDuration * velocityPrediction;

            if (groundLayer.value != 0)
            {
                RaycastHit2D hit = Physics2D.Raycast(
                    new Vector2(baseTarget.x, transform.position.y + 0.5f),
                    Vector2.down,
                    groundRayLength,
                    groundLayer);

                if (hit.collider != null)
                    baseTarget.y = hit.point.y;
            }
        }
        return baseTarget;
    }

    Vector3 GetRestPosition()
    {
        Vector3 worldRest = transform.TransformPoint(_restLocalPos);

        if (groundLayer.value != 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                new Vector2(worldRest.x, transform.position.y + 0.5f),
                Vector2.down,
                groundRayLength,
                groundLayer);

            if (hit.collider != null)
                worldRest.y = hit.point.y;
        }

        return worldRest;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || ikTarget == null) return;

        Gizmos.color = isAirborne ? Color.magenta : (isAttacking ? Color.red : (isStepping ? Color.yellow : (gaitGroup == 0 ? Color.green : Color.blue)));
        Gizmos.DrawWireSphere(_footAnchor, 0.07f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(GetRestPosition(), 0.05f);

        if (isStepping)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(_stepTarget, 0.05f);
            Gizmos.DrawLine(_stepFrom, _stepTarget);
        }
    }
}
