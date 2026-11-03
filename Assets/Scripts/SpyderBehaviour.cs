using UnityEngine;

// Attach to Bone 1 (root bone) of the Spyder.
// Features:
// 1. Terrain Conformance & Gravity: continuously anchors body to groundLayer with slope following; zero floating.
// 2. High-Speed Predatory Pursuit: fast creeping stalk and explosive high-speed skitter bursts.
// 3. Visceral Front-Leg Attack: rears up towering high, raises both front scythes, lunges forward, and slams them down in an aggressive impaling strike.
// 4. Damage / Knockback Reaction (TakeHit): violently flinches backward, slides away with knockback friction, flashes red, and stuns.
// 5. Leap / Pounce Attack: leaps through the air with tucked legs and lands cleanly on terrain near the player.
// 6. Smart Facing & Overhead Avoidance: tilts body up to watch the player jump overhead instead of instantly flipping.
// 7. Insect Posture & Twitching: crouched sprint, rearing threat stance, forward momentum lean, and nervous micro-twitches.
public class SpyderBehaviour : MonoBehaviour
{
    // ─── References ───────────────────────────────────────────────
    [Header("References")]
    [Tooltip("The player's Transform. Auto-found by tag 'Player' if left empty.")]
    public Transform player;

    // ─── Ground Conformance & Gravity ─────────────────────────────
    [Header("Ground Conformance & Gravity")]
    [Tooltip("LayerMask of the ground/terrain colliders.")]
    public LayerMask groundLayer = 1 << 14;
    [Tooltip("Target body height maintained above the ground collider.")]
    public float rideHeight = 1.12f;
    [Tooltip("How fast the body conforms to ground height changes (higher = snappier).")]
    public float groundSnapSpeed = 16f;
    [Tooltip("Downward raycast distance to detect the ground below the body.")]
    public float groundRayLength = 10f;
    [Tooltip("Gravity acceleration applied when falling/airborne.")]
    public float gravity = 25f;

    // ─── Health & Damage Reaction ─────────────────────────────────
    [Header("Health & Damage Reaction")]
    [Tooltip("Maximum health of the spyder.")]
    public int maxHealth = 5;
    [Tooltip("Current health of the spyder.")]
    public int currentHealth = 5;
    [Tooltip("Knockback friction (higher = stops sliding faster).")]
    public float knockbackFriction = 7.0f;
    [Tooltip("Duration of the hit stun / flinch in seconds.")]
    public float hurtDuration = 0.45f;
    [Tooltip("How much the body lifts upward when flinching from damage.")]
    public float hurtLiftHeight = 0.24f;
    [Tooltip("Body backward recoil pitch angle when hit (degrees).")]
    public float hurtTiltAngle = 18.0f;
    [Tooltip("Flash color applied to all spyder sprites upon taking damage.")]
    public Color hitFlashColor = new Color(1f, 0.25f, 0.25f, 1f);

    // ─── Front Leg Attack Animation ───────────────────────────────
    [Header("Front Leg Attack Animation")]
    [Tooltip("IK target for front-left leg (auto-found by name 'Front-F-IK_Target' if empty).")]
    public Transform frontTargetLeft;
    [Tooltip("IK target for front-right leg (auto-found by name 'Front-B-IK_Target' if empty).")]
    public Transform frontTargetRight;
    [Tooltip("LegMover on front-left leg (auto-found on Leg2 if empty).")]
    public LegMover frontMoverLeft;
    [Tooltip("LegMover on front-right leg (auto-found on Leg1 if empty).")]
    public LegMover frontMoverRight;

    [Tooltip("Distance at which the spyder initiates its attack.")]
    public float attackRange = 1.9f;
    [Tooltip("Time between attacks in seconds.")]
    public float attackCooldown = 1.25f;
    [Tooltip("Total duration of the attack animation in seconds.")]
    public float attackDuration = 0.55f;

    [Tooltip("How high the front legs lift into the air during the rear-up telegraph.")]
    public float attackLiftHeight = 1.35f;
    [Tooltip("How far forward the legs reach when stabbing downward into the player.")]
    public float attackReachForward = 0.75f;
    [Tooltip("How high the body lifts when rearing back to strike.")]
    public float attackRearLift = 0.22f;
    [Tooltip("Body backward tilt angle during the rear-up telegraph (degrees).")]
    public float attackRearTilt = 13.0f;
    [Tooltip("Forward lunge distance of the body during the downward strike.")]
    public float attackLungeDistance = 0.4f;

    // ─── High-Speed Creepy Locomotion ─────────────────────────────
    [Header("Creepy Locomotion (Skitter & Stalk)")]
    [Tooltip("Enable realistic arachnid skitter-and-pause locomotion instead of continuous gliding.")]
    public bool enableSkitterCadence = true;
    [Tooltip("Creeping speed when stalking the player from afar.")]
    public float stalkSpeed = 3.0f;
    [Tooltip("Fast sprint speed during aggressive pursuit bursts.")]
    public float skitterSpeed = 7.2f;
    [Tooltip("Distance threshold below which the spider switches from stalking to aggressive bursting.")]
    public float burstDistance = 4.5f;
    [Tooltip("Duration range of an aggressive skitter burst in seconds (min, max).")]
    public Vector2 burstDuration = new Vector2(0.45f, 0.85f);
    [Tooltip("Duration range of a tense freeze/pause between bursts in seconds (min, max).")]
    public Vector2 pauseDuration = new Vector2(0.12f, 0.30f);
    [Tooltip("How smoothly the spyder accelerates during movement (lower = snappier/twitchier).")]
    public float followSmoothing = 0.05f;
    [Tooltip("Minimum gap kept between the spyder and the player.")]
    public float stopDistance = 1.3f;
    [Tooltip("Allow the spyder to chase the player's height. Disable for ground-based movement.")]
    public bool followVerticalMovement = false;

    // ─── Pounce / Leap ────────────────────────────────────────────
    [Header("Pounce / Leap Attack")]
    [Tooltip("Can the spider leap through the air at the player?")]
    public bool canPounce = true;
    [Tooltip("Minimum distance to trigger a leap.")]
    public float minPounceDistance = 2.4f;
    [Tooltip("Maximum distance to trigger a leap.")]
    public float maxPounceDistance = 5.0f;
    [Tooltip("Time between leaps in seconds.")]
    public float pounceCooldown = 3.5f;
    [Tooltip("Peak arc height of the leap in world units.")]
    public float pounceArcHeight = 1.3f;
    [Tooltip("Total flight duration of the leap in seconds.")]
    public float pounceDuration = 0.45f;
    [Tooltip("Crouch anticipation time before launching into the leap.")]
    public float pounceTelegraphTime = 0.2f;

    // ─── Detection Radii ──────────────────────────────────────────
    [Header("Detection")]
    [Tooltip("Distance at which the spyder notices the player and starts following.")]
    public float detectionRadius = 7f;
    [Tooltip("Distance at which the spyder gives up chasing and returns to idle.")]
    public float loseRadius = 10f;

    // ─── Insect Posture & Twitching ───────────────────────────────
    [Header("Arachnid Posture & Twitching")]
    [Tooltip("How much the body lowers during a fast sprint (crouched scuttle).")]
    public float sprintCrouch = 0.07f;
    [Tooltip("Body pitch tilt in the direction of movement (degrees).")]
    public float forwardLeanAngle = 4.5f;
    [Tooltip("Body vertical aim tilt towards player elevation (degrees). Tilts up when player jumps overhead.")]
    public float aimPitchAngle = 14.0f;
    [Tooltip("Intensity of sudden nervous insect micro-twitches (degrees).")]
    public float twitchIntensity = 1.8f;
    [Tooltip("How often micro-twitches occur while stalking or paused (seconds).")]
    public float twitchInterval = 0.25f;

    // ─── Breathing Idle ────────────────────────────────────────────
    [Header("Breathing Idle")]
    [Tooltip("Max distance the body bobs up and down on the Y axis during calm idle.")]
    public float breathAmplitude = 0.03f;
    [Tooltip("How many full breaths per second.")]
    public float breathFrequency = 0.7f;

    // ─── Facing / Flip & Overhead Avoidance ───────────────────────
    [Header("Facing & Overhead Avoidance")]
    [Tooltip("Flip localScale.x when the player is on the opposite side.")]
    public bool flipToFacePlayer = true;
    [Tooltip("Set to false if your spider sprite was drawn facing left at scale.x > 0.")]
    public bool positiveScaleFacesRight = false;
    [Tooltip("Player must be this far past the spider horizontally before a turn triggers.")]
    public float flipThreshold = 0.75f;
    [Tooltip("Delay before turning around to prevent flickering.")]
    public float flipDelay = 0.18f;
    [Tooltip("If the player is this much higher than the spider, they are considered overhead (don't flip).")]
    public float overheadHeightThreshold = 0.7f;
    [Tooltip("Horizontal width around the spider where player is considered overhead.")]
    public float overheadWidthThreshold = 2.2f;

    // ─── Public State ─────────────────────────────────────────────
    public bool isAirborne { get; private set; }
    public bool isAttacking { get; private set; }
    public bool isHurt => _isHurt;

    // ─── Private State ────────────────────────────────────────────
    Vector3 _basePosition; // Grounded master position (X, Y on floor, Z)
    float   _verticalVelocity;
    float   _breathOffset;
    bool    _isFollowing;
    float   _velocityX; // SmoothDamp velocity accumulator for horizontal movement
    float   _knockbackVelocityX;
    bool    _facingRight;
    Vector3 _baseScale;
    Quaternion _baseRotation;
    float   _facingTimer;
    float   _postureOffsetY;
    float   _attackCooldownTimer;
    float   _attackTimer;

    Vector3 _attackAnchorLeft;
    Vector3 _attackAnchorRight;

    // Hurt / Damage state
    bool    _isHurt;
    float   _hurtTimer;
    SpriteRenderer[] _spriteRenderers;
    Color[] _origColors;

    // Skitter cadence
    bool    _isBursting;
    float   _cadenceTimer;

    // Pounce / Leap
    float   _pounceCooldownTimer;
    bool    _isPounceTelegraph;
    float   _pounceTelegraphTimer;
    bool    _isPouncing;
    float   _pounceProgress;
    Vector3 _pounceStart;
    Vector3 _pounceTarget;

    // Twitch state
    float   _twitchTimer;
    float   _currentTwitchZ;
    float   _targetTwitchZ;

    // ──────────────────────────────────────────────────────────────
    void Start()
    {
        _breathOffset = Random.Range(0f, Mathf.PI * 2f);

        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
                player = go.transform;
            else
                Debug.LogWarning("[SpyderBehaviour] No Transform assigned and no 'Player' tag found.", this);
        }

        _baseScale    = transform.localScale;
        _baseRotation = transform.localRotation;
        _facingRight  = positiveScaleFacesRight ? (_baseScale.x >= 0f) : (_baseScale.x < 0f);

        // Auto-detect rideHeight above ground if ground is present
        if (groundLayer.value == 0)
            groundLayer = 1 << 14;

        RaycastHit2D hit = Physics2D.Raycast(transform.position + Vector3.up * 2f, Vector2.down, groundRayLength, groundLayer);
        if (hit.collider != null)
        {
            rideHeight = transform.position.y - hit.point.y;
        }

        _basePosition = transform.position;

        // Cache sprite renderers for damage flash
        var root = transform.root;
        _spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>();
        _origColors = new Color[_spriteRenderers.Length];
        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            _origColors[i] = _spriteRenderers[i].color;
        }

        // Auto-find attack IK targets and LegMovers if not assigned
        if (frontTargetLeft == null)
        {
            var go = GameObject.Find("Front-F-IK_Target");
            if (go != null) frontTargetLeft = go.transform;
        }
        if (frontTargetRight == null)
        {
            var go = GameObject.Find("Front-B-IK_Target");
            if (go != null) frontTargetRight = go.transform;
        }
        if (frontMoverLeft == null)
        {
            var go = GameObject.Find("Leg2");
            if (go != null) frontMoverLeft = go.GetComponent<LegMover>();
        }
        if (frontMoverRight == null)
        {
            var go = GameObject.Find("Leg1");
            if (go != null) frontMoverRight = go.GetComponent<LegMover>();
        }

        _isBursting    = true;
        _cadenceTimer  = Random.Range(burstDuration.x, burstDuration.y);
        _twitchTimer   = twitchInterval;
        _pounceCooldownTimer = 1.5f;
    }

    void OnDisable()
    {
        isAirborne = false;
        if (isAttacking) EndAttack();
        ResetSpriteColors();
    }

    // ──────────────────────────────────────────────────────────────
    void Update()
    {
        float dt = Time.deltaTime;

        // ── 1. Apply knockback horizontal slide ─────────────────
        if (Mathf.Abs(_knockbackVelocityX) > 0.001f)
        {
            _basePosition.x += _knockbackVelocityX * dt;
            _knockbackVelocityX = Mathf.Lerp(_knockbackVelocityX, 0f, dt * knockbackFriction);
        }

        UpdateHurtState();
        UpdateFollowState();

        // ── 2. Movement / State Machine ─────────────────────────
        if (_isHurt)
        {
            _velocityX = 0f;
        }
        else if (_isPouncing)
        {
            UpdatePounceFlight();
        }
        else if (_isPounceTelegraph)
        {
            UpdatePounceTelegraph();
        }
        else if (isAttacking)
        {
            UpdateAttackState();
        }
        else if (_isFollowing)
        {
            CheckAttackTrigger();
            CheckPounceTrigger();
            MoveTowardPlayer();
        }
        else
        {
            _velocityX = 0f;
        }

        // ── 3. Ground Conformance & Gravity ─────────────────────
        if (!_isPouncing)
        {
            RaycastHit2D gHit = Physics2D.Raycast(
                new Vector2(_basePosition.x, _basePosition.y + 2.5f),
                Vector2.down,
                groundRayLength,
                groundLayer);

            if (gHit.collider != null)
            {
                float targetGroundY = gHit.point.y + rideHeight;
                _basePosition.y = Mathf.Lerp(_basePosition.y, targetGroundY, dt * groundSnapSpeed);
                _verticalVelocity = 0f;
                isAirborne = false;
            }
            else
            {
                // Falling: apply gravity
                _verticalVelocity -= gravity * dt;
                _basePosition.y += _verticalVelocity * dt;
                isAirborne = true;
            }
        }

        UpdateCooldowns();
        UpdateTwitch();
    }

    void LateUpdate()
    {
        ApplyPostureAndTilt();
        ApplyFrontLegAttack();
    }

    // ─── Damage / TakeHit Reaction ────────────────────────────────
    public void TakeHit(float knockbackForce, Vector2 knockbackDirection)
    {
        currentHealth = Mathf.Max(0, currentHealth - 1);

        if (_isPouncing)
        {
            _isPouncing = false;
            isAirborne = false;
        }
        _isPounceTelegraph = false;

        if (isAttacking)
        {
            EndAttack();
        }

        // Apply horizontal knockback impulse
        float dirX = Mathf.Sign(knockbackDirection.x);
        _knockbackVelocityX = dirX * knockbackForce;

        _isHurt = true;
        _hurtTimer = hurtDuration;

        TriggerHitFlash();

        if (frontMoverLeft != null) frontMoverLeft.Reanchor();
        if (frontMoverRight != null) frontMoverRight.Reanchor();
    }

    void UpdateHurtState()
    {
        if (!_isHurt) return;

        _hurtTimer -= Time.deltaTime;
        if (_hurtTimer <= 0f)
        {
            _isHurt = false;
            _knockbackVelocityX = 0f;
            ResetSpriteColors();
        }
        else
        {
            float t = 1f - (_hurtTimer / Mathf.Max(hurtDuration, 0.01f));
            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                if (_spriteRenderers[i] != null)
                {
                    _spriteRenderers[i].color = Color.Lerp(hitFlashColor, _origColors[i], t);
                }
            }
        }
    }

    void TriggerHitFlash()
    {
        if (_spriteRenderers == null) return;
        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            if (_spriteRenderers[i] != null)
            {
                _spriteRenderers[i].color = hitFlashColor;
            }
        }
    }

    void ResetSpriteColors()
    {
        if (_spriteRenderers == null || _origColors == null) return;
        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            if (_spriteRenderers[i] != null)
            {
                _spriteRenderers[i].color = _origColors[i];
            }
        }
    }

    // ─── Proximity State Machine ──────────────────────────────────
    void UpdateFollowState()
    {
        if (player == null) return;

        float dist = Vector2.Distance(_basePosition, player.position);

        if (!_isFollowing && dist <= detectionRadius)
        {
            _isFollowing  = true;
            _isBursting   = true;
            _cadenceTimer = Random.Range(burstDuration.x, burstDuration.y);
        }
        else if (_isFollowing && dist > loseRadius && !_isPouncing && !_isPounceTelegraph && !isAttacking && !_isHurt)
        {
            _isFollowing  = false;
            _velocityX    = 0f;
        }
    }

    // ─── Follow Movement (Skitter & Stalk) ─────────────────────────
    void MoveTowardPlayer()
    {
        if (player == null) return;

        float deltaX = player.position.x - _basePosition.x;
        float absDistX = Mathf.Abs(deltaX);

        ApplyFacing(deltaX);

        if (absDistX <= stopDistance)
        {
            _velocityX = 0f;
            return;
        }

        float targetSpeed;

        if (!enableSkitterCadence)
        {
            targetSpeed = stalkSpeed;
        }
        else if (absDistX > burstDistance)
        {
            targetSpeed = stalkSpeed;
        }
        else
        {
            _cadenceTimer -= Time.deltaTime;
            if (_cadenceTimer <= 0f)
            {
                _isBursting = !_isBursting;
                _cadenceTimer = _isBursting
                    ? Random.Range(burstDuration.x, burstDuration.y)
                    : Random.Range(pauseDuration.x, pauseDuration.y);

                if (!_isBursting)
                {
                    _velocityX = 0f;
                    _targetTwitchZ = (Random.value > 0.5f ? 1f : -1f) * twitchIntensity;
                }
            }

            targetSpeed = _isBursting ? skitterSpeed : 0f;
        }

        float targetX = _basePosition.x + Mathf.Sign(deltaX) * (absDistX - stopDistance);
        _basePosition.x = Mathf.SmoothDamp(_basePosition.x, targetX, ref _velocityX, followSmoothing, targetSpeed);
    }

    // ─── Close-Range Attack Trigger & State ───────────────────────
    void CheckAttackTrigger()
    {
        if (player == null || _attackCooldownTimer > 0f || _isPouncing || _isPounceTelegraph || isAttacking || _isHurt)
            return;

        Vector2 toPlayer = (Vector2)player.position - (Vector2)_basePosition;
        if (!followVerticalMovement) toPlayer.y = 0f;

        if (toPlayer.magnitude <= attackRange)
        {
            StartAttack();
        }
    }

    void StartAttack()
    {
        isAttacking = true;
        _attackTimer = Mathf.Max(attackDuration, 0.05f);
        _attackCooldownTimer = attackCooldown;
        _velocityX = 0f;

        if (player != null)
            ApplyFacing(player.position.x - _basePosition.x);

        if (frontTargetLeft != null) _attackAnchorLeft = frontTargetLeft.position;
        if (frontTargetRight != null) _attackAnchorRight = frontTargetRight.position;

        if (frontMoverLeft != null) frontMoverLeft.isAttacking = true;
        if (frontMoverRight != null) frontMoverRight.isAttacking = true;
    }

    void UpdateAttackState()
    {
        _velocityX = 0f;
        _attackTimer -= Time.deltaTime;

        if (_attackTimer <= 0f)
        {
            EndAttack();
        }
    }

    void EndAttack()
    {
        isAttacking = false;
        if (frontMoverLeft != null) frontMoverLeft.Reanchor();
        if (frontMoverRight != null) frontMoverRight.Reanchor();
    }

    // ─── Front Leg Attack IK Animation ────────────────────────────
    void ApplyFrontLegAttack()
    {
        if (!isAttacking) return;

        float p = Mathf.Clamp01(1f - (_attackTimer / Mathf.Max(attackDuration, 0.05f)));
        Vector3 forward = _facingRight ? Vector3.right : Vector3.left;

        Vector3 targetLeftPos = _attackAnchorLeft;
        Vector3 targetRightPos = _attackAnchorRight;

        if (p < 0.38f)
        {
            float u = Mathf.SmoothStep(0f, 1f, p / 0.38f);

            targetLeftPos.y += attackLiftHeight * u;
            targetLeftPos -= forward * (0.22f * u);

            targetRightPos.y += (attackLiftHeight * 0.90f) * u;
            targetRightPos -= forward * (0.16f * u);
        }
        else if (p < 0.68f)
        {
            float u = (p - 0.38f) / 0.30f;
            float slamEase = u * u;

            targetLeftPos.y = Mathf.Lerp(_attackAnchorLeft.y + attackLiftHeight, _attackAnchorLeft.y - 0.12f, slamEase);
            targetLeftPos += forward * Mathf.Lerp(-0.22f, attackReachForward, slamEase);

            float uRight = Mathf.Clamp01((p - 0.41f) / 0.27f);
            float slamEaseRight = uRight * uRight;
            targetRightPos.y = Mathf.Lerp(_attackAnchorRight.y + attackLiftHeight * 0.90f, _attackAnchorRight.y - 0.12f, slamEaseRight);
            targetRightPos += forward * Mathf.Lerp(-0.16f, attackReachForward * 0.88f, slamEaseRight);
        }
        else
        {
            float u = Mathf.SmoothStep(0f, 1f, (p - 0.68f) / 0.32f);

            Vector3 stabEndLeft = _attackAnchorLeft + forward * attackReachForward;
            stabEndLeft.y -= 0.12f;
            targetLeftPos = Vector3.Lerp(stabEndLeft, _attackAnchorLeft, u);

            Vector3 stabEndRight = _attackAnchorRight + forward * (attackReachForward * 0.88f);
            stabEndRight.y -= 0.12f;
            targetRightPos = Vector3.Lerp(stabEndRight, _attackAnchorRight, u);
        }

        if (frontTargetLeft != null) frontTargetLeft.position = targetLeftPos;
        if (frontTargetRight != null) frontTargetRight.position = targetRightPos;
    }

    void UpdateCooldowns()
    {
        if (_attackCooldownTimer > 0f && !isAttacking)
            _attackCooldownTimer -= Time.deltaTime;

        if (_pounceCooldownTimer > 0f)
            _pounceCooldownTimer -= Time.deltaTime;
    }

    // ─── Pounce / Leap ────────────────────────────────────────────
    void CheckPounceTrigger()
    {
        if (!canPounce || player == null || _pounceCooldownTimer > 0f || isAttacking || _isHurt) return;

        float absDistX = Mathf.Abs(player.position.x - _basePosition.x);

        if (absDistX >= minPounceDistance && absDistX <= maxPounceDistance)
        {
            _isPounceTelegraph = true;
            _pounceTelegraphTimer = pounceTelegraphTime;
            _velocityX = 0f;
            ApplyFacing(player.position.x - _basePosition.x);
        }
    }

    void UpdatePounceTelegraph()
    {
        _pounceTelegraphTimer -= Time.deltaTime;
        _velocityX = 0f;

        if (_pounceTelegraphTimer <= 0f)
        {
            _isPounceTelegraph = false;
            LaunchPounce();
        }
    }

    void LaunchPounce()
    {
        _isPouncing       = true;
        isAirborne        = true;
        _pounceProgress   = 0f;
        _pounceStart      = _basePosition;

        float deltaX      = player.position.x - _basePosition.x;
        float leapDist    = Mathf.Max(0.5f, Mathf.Abs(deltaX) - stopDistance * 0.7f);
        float landX       = _basePosition.x + Mathf.Sign(deltaX) * leapDist;

        // Ground raycast at landing spot so spider never lands floating in the air
        RaycastHit2D landHit = Physics2D.Raycast(new Vector2(landX, _basePosition.y + 3f), Vector2.down, groundRayLength, groundLayer);
        float targetY = landHit.collider != null ? (landHit.point.y + rideHeight) : _basePosition.y;

        _pounceTarget     = new Vector3(landX, targetY, _basePosition.z);

        ApplyFacing(deltaX);
    }

    void UpdatePounceFlight()
    {
        _pounceProgress += Time.deltaTime / Mathf.Max(pounceDuration, 0.05f);
        float t = Mathf.Clamp01(_pounceProgress);

        _basePosition = Vector3.Lerp(_pounceStart, _pounceTarget, t);
        _basePosition.y += Mathf.Sin(t * Mathf.PI) * pounceArcHeight;

        if (t >= 1f)
        {
            _basePosition        = _pounceTarget;
            _isPouncing          = false;
            isAirborne           = false;
            _pounceCooldownTimer = pounceCooldown;
            _velocityX           = 0f;

            StartAttack();
        }
    }

    // ─── Posture, Tilt & Nervous Twitches ─────────────────────────
    void ApplyPostureAndTilt()
    {
        float currentSpeed = Mathf.Abs(_velocityX);
        float speedRatio = Mathf.Clamp01(currentSpeed / Mathf.Max(skitterSpeed, 0.01f));
        float facingSign = _facingRight ? 1f : -1f;
        if (!positiveScaleFacesRight) facingSign = -facingSign;
        Vector3 forward = _facingRight ? Vector3.right : Vector3.left;

        _postureOffsetY = 0f;
        float totalZ = 0f;

        if (_isHurt)
        {
            float p = Mathf.Clamp01(_hurtTimer / Mathf.Max(hurtDuration, 0.01f));
            float flinch = Mathf.Sin(p * Mathf.PI);

            _postureOffsetY = hurtLiftHeight * flinch;
            totalZ = -hurtTiltAngle * flinch * facingSign;
        }
        else if (isAttacking)
        {
            float p = Mathf.Clamp01(1f - (_attackTimer / Mathf.Max(attackDuration, 0.05f)));

            if (p < 0.38f)
            {
                float u = Mathf.SmoothStep(0f, 1f, p / 0.38f);
                _postureOffsetY = attackRearLift * u;
                totalZ = -attackRearTilt * u * facingSign;
            }
            else if (p < 0.68f)
            {
                float u = (p - 0.38f) / 0.30f;
                float slamEase = u * u;
                _postureOffsetY = Mathf.Lerp(attackRearLift, -sprintCrouch * 1.5f, slamEase);
                totalZ = Mathf.Lerp(-attackRearTilt, forwardLeanAngle * 2.8f, slamEase) * facingSign;

                _basePosition += forward * (attackLungeDistance * Time.deltaTime / 0.30f);
            }
            else
            {
                float u = Mathf.SmoothStep(0f, 1f, (p - 0.68f) / 0.32f);
                _postureOffsetY = Mathf.Lerp(-sprintCrouch * 1.5f, 0f, u);
                totalZ = Mathf.Lerp(forwardLeanAngle * 2.8f, 0f, u) * facingSign;
            }
        }
        else if (_isPounceTelegraph)
        {
            _postureOffsetY = -sprintCrouch * 2.2f;
        }
        else if (_isPouncing)
        {
            _postureOffsetY = 0f;
        }
        else if (!_isFollowing)
        {
            _postureOffsetY = Mathf.Sin((Time.time * breathFrequency * Mathf.PI * 2f) + _breathOffset) * breathAmplitude;
        }
        else
        {
            float crouch = -sprintCrouch * speedRatio;
            _postureOffsetY = crouch;

            float moveLean = -forwardLeanAngle * speedRatio * facingSign;

            float aimTilt = 0f;
            if (player != null && _isFollowing)
            {
                float dy = player.position.y - _basePosition.y;
                aimTilt = Mathf.Clamp(dy * 4.5f, -aimPitchAngle, aimPitchAngle) * facingSign;
            }

            totalZ = moveLean + aimTilt + _currentTwitchZ;
        }

        // Apply final grounded position + posture offset
        transform.position = _basePosition + Vector3.up * _postureOffsetY;
        transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, totalZ);
    }

    void UpdateTwitch()
    {
        if (_isPouncing || _isPounceTelegraph || isAttacking || _isHurt)
        {
            _currentTwitchZ = 0f;
            return;
        }

        _twitchTimer -= Time.deltaTime;
        if (_twitchTimer <= 0f)
        {
            _twitchTimer = twitchInterval + Random.Range(-0.08f, 0.12f);
            if (Mathf.Abs(_velocityX) < 1f && Random.value < 0.65f)
            {
                _targetTwitchZ = (Random.value > 0.5f ? 1f : -1f) * Random.Range(twitchIntensity * 0.5f, twitchIntensity);
            }
            else
            {
                _targetTwitchZ = 0f;
            }
        }

        _currentTwitchZ = Mathf.Lerp(_currentTwitchZ, _targetTwitchZ, Time.deltaTime * 24f);
    }

    // ─── Smart Facing with Overhead Avoidance ─────────────────────
    void ApplyFacing(float deltaX)
    {
        if (!flipToFacePlayer || _isPouncing || isAttacking || _isHurt) return;

        if (player != null)
        {
            float dy = player.position.y - _basePosition.y;
            float absDx = Mathf.Abs(deltaX);
            if (dy > overheadHeightThreshold && absDx < overheadWidthThreshold)
            {
                _facingTimer = 0f;
                return;
            }
        }

        if (Mathf.Abs(deltaX) < flipThreshold)
        {
            _facingTimer = 0f;
            return;
        }

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
        float absX = Mathf.Abs(s.x);
        if (positiveScaleFacesRight)
            s.x = _facingRight ? absX : -absX;
        else
            s.x = _facingRight ? -absX : absX;

        transform.localScale = s;
    }

    // ─── Scene Gizmos ─────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? _basePosition : transform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, detectionRadius);

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
        Gizmos.DrawWireSphere(center, loseRadius);

        Gizmos.color = new Color(0.9f, 0.1f, 0.9f, 0.6f);
        Gizmos.DrawWireSphere(center, burstDistance);

        if (canPounce)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(center, maxPounceDistance);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(center, minPounceDistance);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, attackRange);

        Gizmos.color = new Color(0.5f, 0f, 0f, 0.8f);
        Gizmos.DrawWireSphere(center, stopDistance);
    }
}
