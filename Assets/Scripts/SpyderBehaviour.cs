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
    [Tooltip("Allow the spyder to chase the player's height. Disable for ground-based movement.")]
    public bool followVerticalMovement = false;

    // ─── Attack ───────────────────────────────────────────────────
    [Header("Front Leg Attack")]
    [Tooltip("First front leg bone to animate during the close-range attack.")]
    public Transform frontLegLeft;
    [Tooltip("Second front leg bone to animate during the close-range attack.")]
    public Transform frontLegRight;
    [Tooltip("Distance at which the spyder starts its front-leg attack.")]
    public float attackRange = 1.5f;
    [Tooltip("Time between front-leg attacks.")]
    public float attackCooldown = 1.25f;
    [Tooltip("Total time of one front-leg attack animation.")]
    public float attackDuration = 0.5f;
    [Tooltip("Local Z rotation applied to the left front leg at the attack peak.")]
    public float leftLegAttackAngle = -35f;
    [Tooltip("Local Z rotation applied to the right front leg at the attack peak.")]
    public float rightLegAttackAngle = 35f;

    // ─── Facing / Flip ────────────────────────────────────────────
    [Header("Facing")]
    [Tooltip("Flip localScale.x when the player is on the opposite side. Disable if your rig handles facing differently.")]
    public bool flipToFacePlayer = true;
    [Tooltip("Horizontal deadzone (world units) before a flip triggers – prevents jitter when player is nearly dead-ahead.")]
    public float flipThreshold   = 0.1f;
    [Tooltip("How long the player must remain on the other side before the spyder flips.")]
    public float flipDelay        = 0.12f;

    // ─── Private state ────────────────────────────────────────────
    float   _breathOffset;   // random phase so multiple spyders don't sync perfectly
    bool    _isFollowing;
    Vector2 _velocity;       // SmoothDamp velocity accumulator
    bool    _facingRight;    // current facing direction
    Vector3 _baseScale;      // original localScale captured at Start
    float   _facingTimer;
    float   _breathY;
    float   _attackCooldownTimer;
    float   _attackTimer;
    Quaternion _frontLegLeftRestRotation;
    Quaternion _frontLegRightRestRotation;

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

        if (frontLegLeft != null)
            _frontLegLeftRestRotation = frontLegLeft.localRotation;
        if (frontLegRight != null)
            _frontLegRightRestRotation = frontLegRight.localRotation;
    }

    // ──────────────────────────────────────────────────────────────
    void Update()
    {
        // Remove the previous frame's bob before movement so it never accumulates.
        Vector3 position = transform.position;
        position.y -= _breathY;
        transform.position = position;

        UpdateFollowState();

        if (_isFollowing)
            MoveTowardPlayer();

        UpdateAttack();
    }

    // Apply breathing after movement and animation so the bob remains visible.
    void LateUpdate()
    {
        ApplyBreathing();
        ApplyFrontLegAttack();
    }

    // ─── Front leg attack ─────────────────────────────────────────
    void UpdateAttack()
    {
        if (_attackCooldownTimer > 0f)
            _attackCooldownTimer -= Time.deltaTime;

        if (_attackTimer > 0f)
        {
            _attackTimer -= Time.deltaTime;
            return;
        }

        if (player == null || _attackCooldownTimer > 0f) return;

        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        if (!followVerticalMovement)
            toPlayer.y = 0f;

        if (toPlayer.magnitude <= attackRange)
        {
            _attackTimer = Mathf.Max(attackDuration, 0.01f);
            _attackCooldownTimer = Mathf.Max(attackCooldown, _attackTimer);
        }
    }

    void ApplyFrontLegAttack()
    {
        if (frontLegLeft == null && frontLegRight == null) return;

        float progress = 1f - (_attackTimer / Mathf.Max(attackDuration, 0.01f));
        float pose = _attackTimer > 0f ? Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) : 0f;

        if (frontLegLeft != null)
        {
            frontLegLeft.localRotation = _frontLegLeftRestRotation
                * Quaternion.Euler(0f, 0f, leftLegAttackAngle * pose);
        }

        if (frontLegRight != null)
        {
            frontLegRight.localRotation = _frontLegRightRestRotation
                * Quaternion.Euler(0f, 0f, rightLegAttackAngle * pose);
        }
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
        if (!followVerticalMovement)
            toPlayer.y = 0f;

        float   dist     = toPlayer.magnitude;

        // Flip body to face the player before moving
        ApplyFacing(toPlayer.x);

        // Don't crowd the player – stay at stopDistance
        if (dist <= stopDistance)
        {
            _velocity = Vector2.zero;
            return;
        }

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

        if (playerIsRight == _facingRight)
        {
            _facingTimer = 0f;
            return;
        }

        _facingTimer += Time.deltaTime;
        if (_facingTimer < flipDelay) return;

        _facingRight = playerIsRight;
        _facingTimer = 0f;
        Vector3 s = _baseScale;
        s.x = _facingRight ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
        transform.localScale = s;
    }

    // ─── Breathing bob ────────────────────────────────────────────
    void ApplyBreathing()
    {
        _breathY = Mathf.Sin((Time.time * breathFrequency * Mathf.PI * 2f) + _breathOffset)
               * breathAmplitude;

        Vector3 pos = transform.position;
        pos.y += _breathY;
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
