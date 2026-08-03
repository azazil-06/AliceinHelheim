using System;
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
    [SerializeField] private float jumpMultiplier=100f;
    [SerializeField] private float lowJumpMultiplier = 2.5f;
    [Header("Attack Movement")]
    [SerializeField] private float attackLungeForce = 15f; 
    [SerializeField] private float lungeFriction = 5f;
    [SerializeField] private float fallSpeedThreshold = 5f;

    //----------------------------------------------------------------------------------

    //hardcoded var
    public Vector2 moveInput;
    bool isGrounded = true;
    bool isMoving;
    bool isFalling = false;

    bool isAttacking = false;
   //------------------------------------------------------------------------------ 
    private float facing = 1f; //idle
    int jumpCount = 2;
    private float attackDirection = 0f;
    

   //------------------------------------------------------------------------------ 

    void Awake()
    {
        control = new InputSystem_Actions();
    }

    

    void OnEnable()
    {
        control.Player.Enable();
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

        //non-event normal functionong
        if(transform.position.y > 4f )
        {
            isGrounded = false;
        }
        else
        {
            isGrounded = true;

                    if (playerRB.linearVelocity.y <= 0.01f) 
                    {
                        jumpCount = 2;
                    }
        }


        playerAnimate.SetBool("isGrounded", isGrounded);


        //--------------------------------------------------------------------------------

        // 2. Uniform Deadzone for both directions (prevents input jitter/flicker)
       isMoving = Mathf.Abs(moveInput.x) > 0.1f;

       //Jump Logic
       checkJump();
       
        
       checkRunAndIdle(); 

       checkAttack();    

       checkFalling(); 
            

        //Animation Logic stays in Update for visual smoothness
        playerAnimate.SetFloat("Motion", moveInput.x);
        playerAnimate.SetFloat("Facing", facing);
        playerAnimate.SetBool("isRunning", isMoving);
    
    }



    void FixedUpdate()
    {
        if (isAttacking){
           float slowedX = Mathf.Lerp(playerRB.linearVelocity.x, 0f, lungeFriction * Time.fixedDeltaTime);
            
            // Apply the slowed X, but leave gravity/Y alone
            playerRB.linearVelocity = new Vector2(slowedX, playerRB.linearVelocity.y);
            
            return;
        }
      if(isGrounded){ playerRB.linearVelocity = new Vector2(moveInput.x * moveSpeed, playerRB.linearVelocity.y); }
      
    }





    //----------------------------------------------------------------------
    //char motions

    void checkRunAndIdle()
    {
         if ( isGrounded && isMoving)
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
         if (control.Player.Jump.triggered && jumpCount > 1)
        {
            
            playerRB.linearVelocity = new Vector2(playerRB.linearVelocity.x, 0f);

            playerRB.AddForce(Vector2.up * jumpMultiplier, ForceMode2D.Impulse);
            playerAnimate.SetBool("isJumping",true); 
            jumpCount--; 
        }    
            //this block pulls down player when spacebar let go
        if (playerRB.linearVelocity.y > 0f && !control.Player.Jump.IsPressed())
        {
            // Apply extra artificial gravity to pull them down faster
            playerRB.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
        }

        if (playerRB.linearVelocity.y <= 0f)
            {
                playerAnimate.SetBool("isJumping", false);
            }
    }

   void checkAttack()
    {
        if (control.Player.Attack.triggered)
        {
            Debug.Log("Attack Input Received!");
            playerAnimate.SetTrigger("Attack"); 
            
            // 1. Stop their current movement immediately to prep for the impulse
            playerRB.linearVelocity = new Vector2(0f, playerRB.linearVelocity.y);

            // 2. If they are holding a direction, apply the lunge impulse
            if (Mathf.Abs(moveInput.x) > 0.1f)
            {
                float direction = Mathf.Sign(moveInput.x);
                playerRB.AddForce(new Vector2(direction * attackLungeForce, 0f), ForceMode2D.Impulse);
            }

            isGrounded = false; // Ensure the player is considered grounded during attack
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


    
    void checkFalling()
    {
        // If the player is in the air and vertical velocity drops below 0 (they are falling)
        if (!isGrounded && transform.position.y > fallSpeedThreshold)
        {
            // Only fire the trigger if we haven't already fired it for this fall
            if (!isFalling)
            {
                playerAnimate.SetTrigger("falling"); // This triggers your falling animation
                isFalling = true;
            }
        }
        
        // Reset the falling state once we touch the ground again
        if (isGrounded)
        {
            isFalling = false;
        }
    }











}
