using UnityEngine;

// Attach to each Leg (Leg1-Leg8) under BodyTarget.
// ikTarget  = the *_IK_Target object  (NOT the effector bone tip)
// opposingLeg = the leg on the opposite side
//
// FIX: Bones flopped/fell during hops because the IK target was being
// displaced by the parent hierarchy before LateUpdate could override it.
// Solution: force ikTarget to world-space anchor FIRST thing every frame,
// and predict the body's movement to place steps ahead of travel direction.
[DefaultExecutionOrder(100)]
public class LegMover : MonoBehaviour
{
    [Header("References")]
    public Transform ikTarget;
    public LegMover opposingLeg;

    [Header("Step Tuning")]
    public float stepDistance     = 0.5f;
    public float stepHeight       = 0.3f;
    public float stepSpeed        = 6f;
    /// <summary>
    /// How far ahead of movement direction the step target is placed.
    /// Increase if feet lag during fast movement. 0 = disabled.
    /// </summary>
    public float velocityPrediction = 0.1f;

    [Header("Bone Stiffness")]
    /// <summary>
    /// When planted, how fast does the foot snap back to the anchor (0=instant, higher=smoother but springy).
    /// Keep at 0 for rigid locking; raise slightly if you want a tiny settle spring.
    /// </summary>
    public float plantSnapSpeed   = 0f;   // 0 = hard lock (recommended to fix flopping)

    [Header("Ground Detection")]
    public LayerMask groundLayer;
    public float groundRayLength  = 5f;

    [Header("Debug (read-only)")]
    public bool isStepping;

    Vector3 _restLocalPos;   // foot rest pos in THIS TRANSFORM's LOCAL space
    Vector3 _footAnchor;     // planted foot world position – locked every frame
    Vector3 _stepFrom;       // world pos where this step started
    Vector3 _stepTarget;     // world pos where this step is going
    float   _t;

    Rigidbody2D _bodyRB;     // optional – used for velocity prediction

    // ─────────────────────────────────────────────────────────────
    void Start()
    {
        if (ikTarget == null)
        {
            Debug.LogError($"[LegMover] {gameObject.name}: ikTarget is not assigned!", this);
            enabled = false;
            return;
        }

        float dist = Vector3.Distance(transform.position, ikTarget.position);
        if (dist < 0.5f)
        {
            Debug.LogError(
                $"[LegMover] {gameObject.name}: ikTarget '{ikTarget.name}' is only {dist:F2} units " +
                $"from the leg mount – it's probably the wrong object (effector instead of IK Target).\n" +
                $"  Leg mount world pos: {transform.position}\n" +
                $"  ikTarget world pos:  {ikTarget.position}\n" +
                $"  → Open each IK Solver (e.g. Front-B-IK) → CCD Solver 2D → 'Target' field → assign THAT object here.",
                this);
        }
        else
        {
            Debug.Log($"[LegMover] {gameObject.name}: ikTarget='{ikTarget.name}' at {ikTarget.position} (dist: {dist:F2}) ✓");
        }

        _restLocalPos = transform.InverseTransformPoint(ikTarget.position);
        _footAnchor   = ikTarget.position;
        _stepFrom     = _footAnchor;
        _stepTarget   = _footAnchor;

        // Walk up the hierarchy to find the body's Rigidbody2D for velocity prediction
        _bodyRB = GetComponentInParent<Rigidbody2D>();
    }

    // ─────────────────────────────────────────────────────────────
    void LateUpdate()
    {
        if (!isStepping)
        {
            // ── PLANTED: hard-lock position every frame ──────────
            // This is the critical fix: write world position directly so no
            // parent-transform displacement can make the bone go floppy.
            if (plantSnapSpeed <= 0f)
            {
                ikTarget.position = _footAnchor;                            // hard lock
            }
            else
            {
                ikTarget.position = Vector3.MoveTowards(
                    ikTarget.position, _footAnchor,
                    plantSnapSpeed * Time.deltaTime);                        // soft snap
            }

            // ── Check if we need a new step ──────────────────────
            Vector3 desired     = GetRestPosition();
            bool otherStepping  = opposingLeg != null && opposingLeg.isStepping;

            if (!otherStepping && Vector3.Distance(desired, _footAnchor) > stepDistance)
            {
                _stepFrom   = _footAnchor;
                _stepTarget = PredictStepTarget(desired);
                _t          = 0f;
                isStepping  = true;
            }
        }
        else
        {
            // ── STEPPING: arc the foot to the target ─────────────
            _t += Time.deltaTime * stepSpeed;
            _t  = Mathf.Clamp01(_t);

            Vector3 pos = Vector3.Lerp(_stepFrom, _stepTarget, _t);
            pos.y += Mathf.Sin(_t * Mathf.PI) * stepHeight;

            ikTarget.position = pos;

            if (_t >= 1f)
            {
                _footAnchor       = _stepTarget;
                ikTarget.position = _footAnchor;
                isStepping        = false;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Offsets the step target in the direction of body travel so the foot
    /// plants ahead of the character rather than behind it.
    /// </summary>
    Vector3 PredictStepTarget(Vector3 baseTarget)
    {
        if (_bodyRB != null && velocityPrediction > 0f)
        {
            Vector2 vel    = _bodyRB.linearVelocity;
            baseTarget.x  += vel.x * velocityPrediction;
            // Keep Y from ground raycast; don't predict Y (avoids airborne misplacement)
        }
        return baseTarget;
    }

    // ─────────────────────────────────────────────────────────────
    /// <summary>Ideal foot position = rest offset rotated with body, snapped to ground.</summary>
    Vector3 GetRestPosition()
    {
        Vector3 worldRest = transform.TransformPoint(_restLocalPos);

        if (groundLayer.value != 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                worldRest + Vector3.up * 0.5f,
                Vector2.down,
                groundRayLength,
                groundLayer);

            if (hit.collider != null)
                worldRest.y = hit.point.y;
        }

        return worldRest;
    }

    // ─────────────────────────────────────────────────────────────
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || ikTarget == null) return;

        Gizmos.color = isStepping ? Color.yellow : Color.green;
        Gizmos.DrawWireSphere(_footAnchor, 0.07f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(GetRestPosition(), 0.05f);

        if (isStepping)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(_stepTarget, 0.05f);
        }
    }
}
