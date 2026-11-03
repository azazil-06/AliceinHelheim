using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // Body declarations
    private InputSystem_Actions control;
    Rigidbody2D playerRB;
    Animator playerAnimate;
    
    //------------------------------------------------------------
    // editable var
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpMultiplier = 6f;
    [SerializeField] private float lowJumpMultiplier = 2.5f;

    [Header("Attack Movement")]
    [SerializeField] private float attackLungeForce = 15f;
    [SerializeField] private float lungeFriction = 5f;

    [Header("Combat Hit Detection")]
    [Tooltip("How far the sledgehammer can reach to hit enemies.")]
    [SerializeField] private float attackRange = 2.4f;
    [Tooltip("Knockback force applied to enemies upon impact.")]
    [SerializeField] private float attackKnockback = 6.5f;
    [Tooltip("Delay in seconds from pressing attack until the hammer slams down.")]
    [SerializeField] private float hitImpactDelay = 0.16f;

    [Header("Falling")]
    [SerializeField] private float fallThreshold = 5f;

    //----------------------------------------------------------------------------------
    // hardcoded var
    public Vector2 moveInput;
    bool isGrounded = true;
    bool isMoving;
    bool isFalling = false;

    bool isAttacking = false;

    bool isJumpPressed = false;
    //------------------------------------------------------------------------------ 
    private float facing = 1f; // 1 = Right, 0 = Left

    //------------------------------------------------------------------------------ 
    void Awake()
    {
        control = new InputSystem_Actions();
    }

    void OnEnable()
    {
        control.Player.Enable();
    }

    void OnDisable()
    {
        control.Player.Disable();
    }

    void Start()
    {
        playerRB = GetComponent<Rigidbody2D>();
        playerAnimate = GetComponent<Animator>();
    }

    void Update()
    {
        // 1. Read Input
        moveInput = new Vector2(control.Player.Move.ReadValue<Vector2>().x, 0f); // ignore y input
        isJumpPressed = control.Player.Jump.IsPressed();

        // Ground check
        if (transform.position.y > 4f)
        {
            isGrounded = false;
        }
        else
        {
            isGrounded = true;
        }

        playerAnimate.SetBool("isGrounded", isGrounded);

        // 2. Uniform Deadzone for both directions
        isMoving = Mathf.Abs(moveInput.x) > 0.1f;

        // Jump & Movement Logic
        checkJump();
        checkRunAndIdle();
        checkAttack();

        // Animation Logic
        playerAnimate.SetFloat("Motion", moveInput.x);
        playerAnimate.SetFloat("Facing", facing);
        playerAnimate.SetBool("isRunning", isMoving);
    }

    void FixedUpdate()
    {
        if (isAttacking)
        {
            float slowedX = Mathf.Lerp(playerRB.linearVelocity.x, 0f, lungeFriction * Time.fixedDeltaTime);
            playerRB.linearVelocity = new Vector2(slowedX, playerRB.linearVelocity.y);
            return;
        }

        // Apply extra gravity while moving up and spacebar is released
        if (playerRB.linearVelocity.y > 0f && !isJumpPressed)
        {
            playerRB.linearVelocity += Vector2.up * Physics2D.gravity.y * playerRB.gravityScale * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }

        if (isGrounded)
        {
            playerRB.linearVelocity = new Vector2(moveInput.x * moveSpeed, playerRB.linearVelocity.y);
        }
    }

    //----------------------------------------------------------------------
    // Character motions

    void checkRunAndIdle()
    {
        if (isGrounded && isMoving)
        {
            if (moveInput.x > 0.1f)
            {
                facing = 1f; // Moving Right
            }
            else if (moveInput.x < -0.1f)
            {
                facing = 0f; // Moving Left
            }
        }
    }

    void checkJump()
    {
        if (isAttacking) return;

        if (control.Player.Jump.triggered && isGrounded)
        {
            playerRB.linearVelocity = new Vector2(playerRB.linearVelocity.x, 0f);
            playerRB.AddForce(Vector2.up * jumpMultiplier, ForceMode2D.Impulse);
            playerAnimate.SetTrigger("isJumping");
        }
    }

    void checkAttack()
    {
        if (isAttacking) return;

        if (control.Player.Attack.triggered)
        {
            playerAnimate.SetTrigger("Attack");

            // 1. Stop current movement immediately to prep for impulse
            playerRB.linearVelocity = new Vector2(0f, playerRB.linearVelocity.y);

            // 2. Determine attack facing direction
            float attackDirX = facing == 1f ? 1f : -1f;
            if (Mathf.Abs(moveInput.x) > 0.1f)
            {
                attackDirX = Mathf.Sign(moveInput.x);
                playerRB.AddForce(new Vector2(attackDirX * attackLungeForce, 0f), ForceMode2D.Impulse);
            }

            // 3. Trigger hit detection coroutine aligned with hammer swing impact
            StartCoroutine(PerformAttackHitCheck(attackDirX));
        }
    }

    private System.Collections.IEnumerator PerformAttackHitCheck(float attackDirX)
    {
        if (hitImpactDelay > 0f)
            yield return new WaitForSeconds(hitImpactDelay);

        Vector2 attackOrigin = transform.position;
        Vector2 hitDir = new Vector2(attackDirX, 0f).normalized;

        var spyders = Object.FindObjectsByType<SpyderBehaviour>(FindObjectsInactive.Exclude);
        foreach (var spyder in spyders)
        {
            Vector2 toSpyder = (Vector2)spyder.transform.position - attackOrigin;
            float dist = toSpyder.magnitude;

            // Check if spyder is within attack reach and in front of player
            bool inFront = (attackDirX > 0f && toSpyder.x > -0.6f) || (attackDirX < 0f && toSpyder.x < 0.6f);

            if (dist <= attackRange && inFront)
            {
                spyder.TakeHit(attackKnockback, hitDir);
            }
        }
    }

    public void LockMovement()
    {
        isAttacking = true;
    }

    public void UnlockMovement()
    {
        isAttacking = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Vector3 origin = transform.position;
        Vector3 dir = (facing == 1f ? Vector3.right : Vector3.left) * attackRange;
        Gizmos.DrawRay(origin, dir);
        Gizmos.DrawWireSphere(origin + dir * 0.5f, attackRange * 0.5f);
    }
}
