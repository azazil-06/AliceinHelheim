using UnityEngine;

// Attach to each Leg (Leg1-Leg8) under BodyTarget.
// ikTarget  = the *_IK_Target object  (NOT the effector bone tip)
// opposingLeg = the leg on the opposite side
[DefaultExecutionOrder(100)]
public class LegMover : MonoBehaviour
{
    [Header("References")]
    public Transform ikTarget;
    public LegMover opposingLeg;

    [Header("Step Tuning")]
    public float stepDistance = 0.5f;
    public float stepHeight = 0.3f;
    public float stepSpeed = 6f;

    [Header("Ground Detection")]
    public LayerMask groundLayer;
    public float groundRayLength = 5f;

    [Header("Debug (read-only)")]
    public bool isStepping;

    Vector3 _restLocalPos;   // foot rest pos in THIS TRANSFORM's LOCAL space (handles body rotation)
    Vector3 _footAnchor;     // planted foot world position - locked every frame
    Vector3 _stepFrom;       // world pos where this step started
    Vector3 _stepTarget;     // world pos where this step is going
    float _t;

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
                $"from the leg mount - it's probably the wrong object (effector instead of IK Target).\n" +
                $"  Leg mount world pos: {transform.position}\n" +
                $"  ikTarget world pos:  {ikTarget.position}\n" +
                $"  → Open each IK Solver (e.g. Front-B-IK) → CCD Solver 2D → 'Target' field → assign THAT object here.",
                this);
        }
        else
        {
            Debug.Log($"[LegMover] {gameObject.name}: ikTarget='{ikTarget.name}' at {ikTarget.position} (dist from mount: {dist:F2}) ✓");
        }

        // Store in LOCAL space so TransformPoint gives the correct world pos
        // regardless of how the body rotates
        _restLocalPos = transform.InverseTransformPoint(ikTarget.position);

        _footAnchor = ikTarget.position;
        _stepFrom = _footAnchor;
        _stepTarget = _footAnchor;
    }

    void LateUpdate()
    {
        if (!isStepping)
        {
            // Keep foot firmly planted - resist parent bone dragging it
            ikTarget.position = _footAnchor;

            Vector3 desired = GetRestPosition();
            bool otherStepping = opposingLeg != null && opposingLeg.isStepping;

            if (!otherStepping && Vector3.Distance(desired, _footAnchor) > stepDistance)
            {
                _stepFrom = _footAnchor;
                _stepTarget = desired;
                _t = 0f;
                isStepping = true;
            }
        }
        else
        {
            _t += Time.deltaTime * stepSpeed;
            _t = Mathf.Clamp01(_t);

            Vector3 pos = Vector3.Lerp(_stepFrom, _stepTarget, _t);
            pos.y += Mathf.Sin(_t * Mathf.PI) * stepHeight;

            ikTarget.position = pos;

            if (_t >= 1f)
            {
                _footAnchor = _stepTarget;
                ikTarget.position = _footAnchor;
                isStepping = false;
            }
        }
    }

    // Ideal foot position = rest offset rotated with the body, snapped to ground
    Vector3 GetRestPosition()
    {
        // TransformPoint converts local→world correctly even when body is rotated/scaled
        Vector3 worldRest = transform.TransformPoint(_restLocalPos);

        if (groundLayer.value != 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(worldRest + Vector3.up * 0.5f,
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
        Gizmos.color = isStepping ? Color.yellow : Color.green;
        Gizmos.DrawWireSphere(_footAnchor, 0.07f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(GetRestPosition(), 0.05f);
    }
}
