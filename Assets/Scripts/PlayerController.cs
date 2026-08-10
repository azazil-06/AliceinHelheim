using UnityEngine;

public class PlayerController : MonoBehaviour
{
    //Body declarations
    private InputSystem_Actions control;
    Rigidbody2D playerRB;
    Animator playerAnimate;
    
    //------------------------------------------------------------

    //editable var
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpMultiplier = 6f;
    [SerializeField] private float lowJumpMultiplier = 2.5f;
    [Header("Attack Movement")]
    [SerializeField] private float attackLungeForce = 15f;
    [SerializeField] private float lungeFriction = 5f;
    [Header("Falling")]
    [SerializeField] private float fallThreshold = 5f;

    //----------------------------------------------------------------------------------

    //hardcoded var
    public Vector2 moveInput;
    bool isGrounded = true;
    bool isMoving;
    bool isFalling = false;

    bool isAttacking = false;

    bool isJumpPressed = false;
   //------------------------------------------------------------------------------ 
    private float facing = 1f; //idle


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
        moveInput = new Vector2(control.Player.Move.ReadValue<Vector2>().x, 0f); //ignore y input
        isJumpPressed = control.Player.Jump.IsPressed();

        //non-event normal functionong
        if (transform.position.y > 4f)
        {
            isGrounded = false;
        }
        else
        {
            isGrounded = true;
        }

        playerAnimate.SetBool("isGrounded", isGrounded);

        //--------------------------------------------------------------------------------

        // 2. Uniform Deadzone for both directions (prevents input jitter/flicker)
        isMoving = Mathf.Abs(moveInput.x) > 0.1f;

        //Jump Logic
        checkJump();

        checkRunAndIdle();

        checkAttack();


        //Animation Logic stays in Update for visual smoothness
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

            // NEW: Apply the extra gravity here while moving up and spacebar is released
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
    //char motions

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

            // 1. Stop their current movement immediately to prep for the impulse
            playerRB.linearVelocity = new Vector2(0f, playerRB.linearVelocity.y);

            // 2. If they are holding a direction, apply the lunge impulse
            if (Mathf.Abs(moveInput.x) > 0.1f)
            {
                float direction = Mathf.Sign(moveInput.x);
                playerRB.AddForce(new Vector2(direction * attackLungeForce, 0f), ForceMode2D.Impulse);
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

 
}