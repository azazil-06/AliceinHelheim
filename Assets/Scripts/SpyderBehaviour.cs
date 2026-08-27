using UnityEngine;

// Attach to Bone 1 (root bone) of the Spyder.
// – Applies a subtle sinusoidal Y-axis "breathing" bob.
// – When the player walks within detectionRadius, the spyder follows them.
//   When the player leaves loseRadius it stops and idles again.
public class SpyderBehaviour : MonoBehaviour
{
    // ─── References ───────────────────────────────────────────────
    [Header("References")]
    [Tooltip("The player's Transform. Auto-found by tag 'Player' if left empty.")]
    public Transform player;

    // ─── Breathing ────────────────────────────────────────────────
    [Header("Breathing Idle")]
    [Tooltip("Max distance the body bobs up and down on the Y axis.")]
    public float breathAmplitude = 0.08f;
    [Tooltip("How many full breaths per second.")]
    public float breathFrequency = 0.8f;

    // ─── Following ────────────────────────────────────────────────
    [Header("Player Following")]
    [Tooltip("Distance at which the spyder notices the player and starts following.")]
    public float detectionRadius = 5f;
    [Tooltip("Distance at which the spyder gives up chasing and returns to idle.")]
    public float loseRadius      = 8f;
    [Tooltip("Movement speed while following.")]
    public float followSpeed     = 2.5f;
    [Tooltip("How smoothly the spyder accelerates/decelerates (lower = snappier).")]
    public float followSmoothing = 0.15f;
    [Tooltip("Minimum gap kept between the spyder and the player.")]
    public float stopDistance    = 1.2f;

    // ─── Facing / Flip ────────────────────────────────────────────
    [Header("Facing")]
    [Tooltip("Flip localScale.x when the player is on the opposite side. Disable if your rig handles facing differently.")]
    public bool flipToFacePlayer = true;
    [Tooltip("Horizontal deadzone (world units) before a flip triggers – prevents jitter when player is nearly dead-ahead.")]
    public float flipThreshold   = 0.1f;

    // ─── Private state ────────────────────────────────────────────
    float   _breathOffset;   // random phase so multiple spyders don't sync perfectly
    bool    _isFollowing;
    Vector2 _velocity;       // SmoothDamp velocity accumulator
    bool    _facingRight;    // current facing direction
    Vector3 _baseScale;      // original localScale captured at Start

    Rigidbody2D _rb;         // optional – used for physics-based movement if present

    // ──────────────────────────────────────────────────────────────
    void Start()
    {
        // Random breath phase so a room full of spyders breathes out of sync
        _breathOffset = Random.Range(0f, Mathf.PI * 2f);

        // Auto-find player by tag if not manually assigned
        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
                player = go.transform;
            else
                Debug.LogWarning("[SpyderBehaviour] No Transform assigned and no 'Player' tag found.", this);
        }

        _rb = GetComponent<Rigidbody2D>();

        // Capture the original scale so we can mirror it cleanly
        _baseScale    = transform.localScale;
        _facingRight  = _baseScale.x >= 0f;
    }

    // ──────────────────────────────────────────────────────────────
    void Update()
    {
        UpdateFollowState();

        if (_isFollowing)
            MoveTowardPlayer();

        // Breathing applied after movement so it layers on top
        ApplyBreathing();
    }

    // ─── Proximity state machine ──────────────────────────────────
    void UpdateFollowState()
    {
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (!_isFollowing && dist <= detectionRadius)
        {
            _isFollowing = true;
        }
        else if (_isFollowing && dist > loseRadius)
        {
            _isFollowing = false;
            _velocity    = Vector2.zero; // reset so there's no leftover momentum
        }
    }

    // ─── Follow movement ──────────────────────────────────────────
    void MoveTowardPlayer()
    {
        if (player == null) return;

        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        float   dist     = toPlayer.magnitude;

        // Flip body to face the player before moving
        ApplyFacing(toPlayer.x);

        // Don't crowd the player – stay at stopDistance
        if (dist <= stopDistance) return;

        Vector2 targetPos = (Vector2)transform.position + toPlayer.normalized * (dist - stopDistance);

        if (_rb != null)
        {
            // Physics body: drive with MovePosition
            Vector2 next = Vector2.SmoothDamp(
                (Vector2)transform.position,
                targetPos,
                ref _velocity,
                followSmoothing,
                followSpeed);
            _rb.MovePosition(next);
        }
        else
        {
            // No Rigidbody – move the transform directly (good for root bones)
            Vector2 next = Vector2.SmoothDamp(
                (Vector2)transform.position,
                targetPos,
                ref _velocity,
                followSmoothing,
                followSpeed);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }
    }

    // ─── Facing flip ──────────────────────────────────────────────
    // Flips localScale.x (not Y-rotation) so the IK chain is unaffected.
    // flipThreshold is a small deadzone to prevent jitter at dead-center.
    void ApplyFacing(float deltaX)
    {
        if (!flipToFacePlayer) return;
        if (Mathf.Abs(deltaX) < flipThreshold) return;

        bool playerIsRight = deltaX > 0f;

        if (playerIsRight != _facingRight)
        {
            _facingRight = playerIsRight;
            Vector3 s = _baseScale;
            s.x = _facingRight ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
            transform.localScale = s;
        }
    }

    // ─── Breathing bob ────────────────────────────────────────────
    void ApplyBreathing()
    {
        float breathY = Mathf.Sin((Time.time * breathFrequency * Mathf.PI * 2f) + _breathOffset)
                        * breathAmplitude;

        // Add to whatever position movement already set this frame
        Vector3 pos = transform.position;
        pos.y += breathY;
        transform.position = pos;
    }

    // ─── Scene gizmos ─────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        // Detection radius – yellow
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Lose radius – orange
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, loseRadius);

        // Stop distance – red
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}
