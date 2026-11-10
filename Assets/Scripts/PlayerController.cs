using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // Body declarations
    private InputSystem_Actions control;
    private Rigidbody2D playerRB;
    private Collider2D playerCollider;

    [Header("Active Animator")]
    [Tooltip("Active 3D character Animator under Visual.")]
    [SerializeField] private Animator playerAnimate;

    //------------------------------------------------------------
    // Editable vars - Movement & Jump
    [Header("Locomotion")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpMultiplier = 6f;
    [SerializeField] private float lowJumpMultiplier = 2.5f;

    [Header("3D Visual & 2.5D Orientation")]
    [Tooltip("Child transform that holds the 3D character model. If empty, automatically looks for a child named 'Visual' or with an Animator.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Interpolation speed for turning yaw between left and right (used if Smooth Turning is enabled).")]
    [SerializeField] private float turnSpeed = 30f;
    [Tooltip("Target yaw rotation when idle and facing right (in degrees). Balances Idle clip's slight leftward head/torso twist for crisp +X right facing.")]
    [SerializeField] private float idleRightYaw = 105f;
    [Tooltip("Target yaw rotation when running, jumping, or attacking right (in degrees).")]
    [SerializeField] private float moveRightYaw = 90f;
    [Tooltip("Target yaw rotation when facing left (in degrees).")]
    [SerializeField] private float leftYaw = 270f;
    [Tooltip("Instant flip (classic crisp sidescroller feel) or smooth rotation interpolation.")]
    [SerializeField] private bool smoothTurning = false;

    [Header("Ground Check (2.5D Physics)")]
    [Tooltip("LayerMask of the ground/terrain (Layer 14 'base').")]
    [SerializeField] private LayerMask groundLayer = 1 << 14;
    [Tooltip("Downward distance to check for ground beneath the player collider.")]
    [SerializeField] private float groundCheckDistance = 0.15f;
    [Tooltip("Radius of the circle cast for ground detection.")]
    [SerializeField] private float groundCheckRadius = 0.2f;
    [Tooltip("Offset from bottom of collider for raycast origin.")]
    [SerializeField] private float groundCheckOffset = 0.05f;

    [Header("Combat & Combos")]
    [Tooltip("How long (in seconds) the character stays in Fighting Idle before returning to normal Idle.")]
    [SerializeField] private float combatIdleDuration = 3.0f;
    [Tooltip("Time window (in seconds) after an attack to chain into the next combo attack.")]
    [SerializeField] private float comboWindow = 0.9f;
    [SerializeField] private float runningKickLunge = 18f;
    [SerializeField] private float boxingLunge = 6f;
    [SerializeField] private float kickLunge = 12f;
    [SerializeField] private float lungeFriction = 5f;
    [Tooltip("How far the attack can reach to hit enemies.")]
    [SerializeField] private float attackRange = 2.4f;
    [Tooltip("Knockback force applied to enemies upon impact.")]
    [SerializeField] private float attackKnockback = 6.5f;
    [Tooltip("Delay in seconds from pressing attack until the impact occurs.")]
    [SerializeField] private float hitImpactDelay = 0.16f;
    [Tooltip("Optional weapon attachment point in the 3D rig's hand.")]
    [SerializeField] private Transform weaponSocket;

    [Header("Falling & Air State")]
    [SerializeField] private float fallThreshold = -0.5f;

    //----------------------------------------------------------------------------------
    // State vars
    public Vector2 moveInput;
    public bool isGrounded = true;
    public bool isMoving;
    public bool isFalling = false;
    public bool isAttacking = false;
    public bool inCombat = false;
    private bool isJumpPressed = false;
    private float facing = 1f; // 1 = Right, 0 = Left
    private float targetYaw = 105f;
    private int currentComboIndex = 0; // 0 = Boxing 1, 1 = Boxing 2, 2 = Kick, 3 = Run to kick
    private float lastAttackTime = -10f;
    private float lastJumpTime = -10f;
    private float lastGroundedTime = -10f;
    private float combatTimer = 0f;

    // Cached Animator parameter names for the 3D character controller
    private readonly HashSet<string> _animParams = new HashSet<string>();

    //------------------------------------------------------------------------------ 
    void Awake()
    {
        control = new InputSystem_Actions();
        // Enforce upright identity rotation on Player root for strict 2D physics plane
        transform.rotation = Quaternion.identity;

        ResolveActiveAnimator();
    }

    void OnEnable()
    {
        if (control == null) control = new InputSystem_Actions();
        control.Player.Enable();
    }

    void OnDisable()
    {
        if (control != null) control.Player.Disable();
    }

    void Start()
    {
        playerRB = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();

        ResolveActiveAnimator();
        UpdateTargetRotation(true);
    }

    public void ResolveActiveAnimator()
    {
        if (visualRoot == null)
        {
            visualRoot = transform.Find("Visual");
        }

        if (visualRoot != null)
        {
            var anim = visualRoot.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                playerAnimate = anim;
            }
        }

        if (playerAnimate != null)
        {
            playerAnimate.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            smr.updateWhenOffscreen = true;
        }

        CacheAnimatorParameters();
    }

    /// <summary>
    /// Inspects and caches all available parameter names on the active 3D Animator.
    /// </summary>
    public void CacheAnimatorParameters()
    {
        _animParams.Clear();
        if (playerAnimate != null && playerAnimate.runtimeAnimatorController != null)
        {
            foreach (var p in playerAnimate.parameters)
            {
                _animParams.Add(p.name);
            }
        }
    }

    void Update()
    {
        if (control == null)
        {
            control = new InputSystem_Actions();
            control.Player.Enable();
        }

        // 1. Read Input
        moveInput = new Vector2(control.Player.Move.ReadValue<Vector2>().x, 0f);
        isJumpPressed = control.Player.Jump.IsPressed();
        isMoving = Mathf.Abs(moveInput.x) > 0.1f;

        // Update Fighting Idle combat timeout
        if (inCombat)
        {
            combatTimer -= Time.deltaTime;
            if (combatTimer <= 0f)
            {
                inCombat = false;
                currentComboIndex = 0;
            }
        }

        // 2. Perform 2.5D Ground Check
        CheckGround();

        // 3. Movement, Jump & Combat Logic
        CheckRunAndIdle();
        CheckJump();
        CheckAttack();

        // 4. Update 3D Visual Yaw Rotation
        UpdateVisualRotation();

        // 5. Send Animation Parameters
        UpdateAnimator();
    }

    void FixedUpdate()
    {
        if (isAttacking)
        {
            float slowedX = Mathf.Lerp(playerRB.linearVelocity.x, 0f, lungeFriction * Time.fixedDeltaTime);
            playerRB.linearVelocity = new Vector2(slowedX, playerRB.linearVelocity.y);
            return;
        }

        // Apply extra gravity while moving up and jump button is released (variable jump height)
        if (playerRB.linearVelocity.y > 0f && !isJumpPressed)
        {
            playerRB.linearVelocity += Vector2.up * Physics2D.gravity.y * playerRB.gravityScale * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }

        // Horizontal velocity
        if (isGrounded)
        {
            playerRB.linearVelocity = new Vector2(moveInput.x * moveSpeed, playerRB.linearVelocity.y);
        }
        else if (Mathf.Abs(moveInput.x) > 0.1f)
        {
            // Responsive mid-air steering / velocity preservation while holding direction
            float targetVelX = moveInput.x * moveSpeed;
            float newVelX = Mathf.MoveTowards(playerRB.linearVelocity.x, targetVelX, moveSpeed * 5f * Time.fixedDeltaTime);
            playerRB.linearVelocity = new Vector2(newVelX, playerRB.linearVelocity.y);
        }
    }

    //----------------------------------------------------------------------
    // Ground & Falling Detection

    void CheckGround()
    {
        // Don't flag grounded during initial jump liftoff (first 0.15s or while ascending)
        if (Time.time - lastJumpTime < 0.15f || (playerRB != null && playerRB.linearVelocity.y > 0.5f))
        {
            isGrounded = false;
            isFalling = false;
            return;
        }

        Vector2 checkOrigin;
        float radius = groundCheckRadius;

        if (playerCollider != null)
        {
            Bounds b = playerCollider.bounds;
            checkOrigin = new Vector2(b.center.x, b.min.y + groundCheckOffset);
            radius = Mathf.Min(groundCheckRadius, b.extents.x * 0.9f);
        }
        else
        {
            checkOrigin = (Vector2)transform.position + Vector2.up * groundCheckOffset;
        }

        RaycastHit2D hit = Physics2D.CircleCast(checkOrigin, radius, Vector2.down, groundCheckDistance, groundLayer);
        isGrounded = (hit.collider != null);
        if (isGrounded)
        {
            lastGroundedTime = Time.time;
        }

        // Falling state
        isFalling = (!isGrounded && playerRB != null && playerRB.linearVelocity.y < fallThreshold);
    }

    //----------------------------------------------------------------------
    // Character Motions & 2.5D Orientation

    void CheckRunAndIdle()
    {
        if (isMoving)
        {
            if (moveInput.x > 0.1f && facing != 1f)
            {
                facing = 1f; // Moving Right
                UpdateTargetRotation(false);
            }
            else if (moveInput.x < -0.1f && facing != 0f)
            {
                facing = 0f; // Moving Left
                UpdateTargetRotation(false);
            }
        }

        // Refresh yaw target when shifting between moving and idle
        UpdateTargetRotation(false);
    }

    void UpdateTargetRotation(bool instant)
    {
        // 2.5D Sidescroller Yaw Orientation:
        // When facing right:
        // - Idle: idleRightYaw (105°) aligns head/torso symmetrically with Left Idle (270°)
        // - Move / Combat / Jump: moveRightYaw (90°) faces straight along +X
        // When facing left:
        // - leftYaw (270°) ("left the same as before")
        if (facing == 1f)
        {
            bool isIdle = !isMoving && isGrounded && !inCombat && !isAttacking;
            targetYaw = isIdle ? idleRightYaw : moveRightYaw;
        }
        else
        {
            targetYaw = leftYaw;
        }

        if (instant && visualRoot != null)
        {
            visualRoot.localRotation = Quaternion.Euler(0f, targetYaw, 0f);
        }
    }

    void UpdateVisualRotation()
    {
        if (visualRoot == null) return;

        Quaternion targetRot = Quaternion.Euler(0f, targetYaw, 0f);

        // If smoothTurning is enabled OR if adjusting slightly between idle and move yaw on the right, blend smoothly
        bool isMinorAngleAdjustment = Mathf.Abs(Mathf.DeltaAngle(visualRoot.localEulerAngles.y, targetYaw)) < 65f;
        if (smoothTurning || isMinorAngleAdjustment)
        {
            visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, targetRot, turnSpeed * Time.deltaTime);
        }
        else
        {
            // Crisp instant 180° flip between Left and Right
            visualRoot.localRotation = targetRot;
        }
    }

    void CheckJump()
    {
        if (isAttacking) return;

        bool canJump = isGrounded || (Time.time - lastGroundedTime <= 0.12f && Time.time - lastJumpTime >= 0.2f);
        if (control.Player.Jump.triggered && canJump)
        {
            playerRB.linearVelocity = new Vector2(playerRB.linearVelocity.x, 0f);
            playerRB.AddForce(Vector2.up * jumpMultiplier, ForceMode2D.Impulse);

            bool isRunningJump = isMoving || Mathf.Abs(playerRB.linearVelocity.x) > 0.5f;
            lastJumpTime = Time.time;
            lastGroundedTime = -10f;
            isGrounded = false;

            SafeSetBool("IsRunningJump", isRunningJump);
            SafeSetBool("IsGrounded", false);
            SafeSetTrigger("JumpTrigger");
        }
    }

    void CheckAttack()
    {
        if (isAttacking) return;

        if (control.Player.Attack.triggered)
        {
            // Determine attack facing direction
            float attackDirX = facing == 1f ? 1f : -1f;
            if (Mathf.Abs(moveInput.x) > 0.1f)
            {
                attackDirX = Mathf.Sign(moveInput.x);
                facing = attackDirX > 0f ? 1f : 0f;
                UpdateTargetRotation(true); // Snap orientation towards strike direction
            }

            float lunge;
            float reach;
            float knockback;
            float delay;

            // Stop current horizontal velocity
            playerRB.linearVelocity = new Vector2(0f, playerRB.linearVelocity.y);

            if (isMoving)
            {
                // Run to kick (running attack)
                currentComboIndex = 3;
                lunge = runningKickLunge;
                reach = attackRange * 1.25f;
                knockback = attackKnockback * 1.3f;
                delay = hitImpactDelay * 1.2f;
            }
            else
            {
                // Stationary combo: Boxing 1 -> Boxing 2 -> Kick
                if (Time.time - lastAttackTime <= comboWindow)
                {
                    if (currentComboIndex == 0)
                        currentComboIndex = 1; // Boxing 2
                    else if (currentComboIndex == 1)
                        currentComboIndex = 2; // Kick finisher
                    else
                        currentComboIndex = 0; // Boxing 1
                }
                else
                {
                    currentComboIndex = 0; // Boxing 1
                }

                if (currentComboIndex == 2)
                {
                    // Kick finisher
                    lunge = kickLunge;
                    reach = attackRange * 1.15f;
                    knockback = attackKnockback * 1.4f;
                    delay = hitImpactDelay * 1.3f;
                }
                else
                {
                    // Boxing punches
                    lunge = boxingLunge;
                    reach = attackRange * 0.9f;
                    knockback = attackKnockback * 0.8f;
                    delay = hitImpactDelay;
                }
            }

            lastAttackTime = Time.time;
            combatTimer = combatIdleDuration;
            inCombat = true;

            // Apply lunge impulse in strike direction
            playerRB.AddForce(new Vector2(attackDirX * lunge, 0f), ForceMode2D.Impulse);

            // Trigger animation
            SafeSetInteger("AttackIndex", currentComboIndex);
            SafeSetTrigger("AttackTrigger");

            StartCoroutine(PerformAttackHitCheck(attackDirX, reach, knockback, delay));
        }
    }

    private System.Collections.IEnumerator PerformAttackHitCheck(float attackDirX, float reach, float knockback, float delay)
    {
        isAttacking = true;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Vector2 attackOrigin = transform.position;
        Vector2 hitDir = new Vector2(attackDirX, 0f).normalized;

        var spyders = Object.FindObjectsByType<SpyderBehaviour>(FindObjectsInactive.Exclude);
        foreach (var spyder in spyders)
        {
            Vector2 toSpyder = (Vector2)spyder.transform.position - attackOrigin;
            float dist = toSpyder.magnitude;

            // Check if spyder is within attack reach and in front of player
            bool inFront = (attackDirX > 0f && toSpyder.x > -0.6f) || (attackDirX < 0f && toSpyder.x < 0.6f);

            if (dist <= reach && inFront)
            {
                spyder.TakeHit(knockback, hitDir);
            }
        }

        // Brief delay before character unlocks movement after strike
        yield return new WaitForSeconds(0.18f);
        isAttacking = false;
    }

    //----------------------------------------------------------------------
    // Animation Bridge

    void UpdateAnimator()
    {
        if (playerAnimate == null) return;

        float speedValue = Mathf.Abs(moveInput.x);

        SafeSetFloat("Speed", speedValue);
        SafeSetFloat("MoveSpeed", speedValue);
        SafeSetBool("IsGrounded", isGrounded);
        SafeSetBool("IsMoving", isMoving);
        SafeSetBool("IsFalling", isFalling);
        SafeSetBool("InCombat", inCombat);
        SafeSetInteger("AttackIndex", currentComboIndex);
    }

    private void SafeSetFloat(string paramName, float value)
    {
        if (playerAnimate == null) return;
        if (_animParams.Count == 0) CacheAnimatorParameters();
        if (_animParams.Contains(paramName))
            playerAnimate.SetFloat(paramName, value);
    }

    private void SafeSetBool(string paramName, bool value)
    {
        if (playerAnimate == null) return;
        if (_animParams.Count == 0) CacheAnimatorParameters();
        if (_animParams.Contains(paramName))
            playerAnimate.SetBool(paramName, value);
    }

    private void SafeSetInteger(string paramName, int value)
    {
        if (playerAnimate == null) return;
        if (_animParams.Count == 0) CacheAnimatorParameters();
        if (_animParams.Contains(paramName))
            playerAnimate.SetInteger(paramName, value);
    }

    private void SafeSetTrigger(string paramName)
    {
        if (playerAnimate == null) return;
        if (_animParams.Count == 0) CacheAnimatorParameters();
        if (_animParams.Contains(paramName))
            playerAnimate.SetTrigger(paramName);
    }

    //----------------------------------------------------------------------
    // Public Animation Event Callbacks

    public void LockMovement()
    {
        isAttacking = true;
    }

    public void UnlockMovement()
    {
        isAttacking = false;
    }

    public void SetVisualRoot(Transform visual)
    {
        visualRoot = visual;
        if (visualRoot != null)
        {
            playerAnimate = visualRoot.GetComponentInChildren<Animator>();
        }
        CacheAnimatorParameters();
        UpdateTargetRotation(true);
    }

    //----------------------------------------------------------------------
    // Gizmos

    void OnDrawGizmosSelected()
    {
        // 1. Attack Range Gizmo
        Gizmos.color = Color.magenta;
        Vector3 origin = transform.position;
        Vector3 dir = (facing == 1f ? Vector3.right : Vector3.left) * attackRange;
        Gizmos.DrawRay(origin, dir);
        Gizmos.DrawWireSphere(origin + dir * 0.5f, attackRange * 0.5f);

        // 2. Ground Check Gizmo
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector2 checkOrigin;
        float radius = groundCheckRadius;

        if (playerCollider != null)
        {
            Bounds b = playerCollider.bounds;
            checkOrigin = new Vector2(b.center.x, b.min.y + groundCheckOffset);
            radius = Mathf.Min(groundCheckRadius, b.extents.x * 0.9f);
        }
        else
        {
            checkOrigin = (Vector2)transform.position + Vector2.up * groundCheckOffset;
        }

        Gizmos.DrawWireSphere(checkOrigin, radius);
        Gizmos.DrawWireSphere(checkOrigin + Vector2.down * groundCheckDistance, radius);
        Gizmos.DrawLine(checkOrigin, checkOrigin + Vector2.down * groundCheckDistance);
    }
}
