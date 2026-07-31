using System;
using UnityEngine;

//SourceItems contains all og char animations and background

public class PlayerController : MonoBehaviour
{
    //Body declarations
    private InputSystem_Actions control;
    Rigidbody2D playerRB;
    Animator playerAnimate;

    //editable var
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpMultiplier=100f;

    //hardcoded var
    public Vector2 moveInput;
    bool isGrounded = true;
    
    private float facing = 1f; //idle

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

        if(transform.position.y > 4f )
        {
            isGrounded = false;
        }
        else
        {
            isGrounded = true;
        }
        playerAnimate.SetBool("isGrounded", isGrounded);
        playerAnimate.SetBool("isJumping", false);

        // 2. Uniform Deadzone for both directions (prevents input jitter/flicker)
        bool isMoving = Mathf.Abs(moveInput.x) > 0.1f;

       // 3. Jump Logic
        if (control.Player.Jump.triggered)
        {
            // Use Impulse for an instant vertical boost
            playerRB.AddForce(Vector2.up * jumpMultiplier, ForceMode2D.Impulse);
            
            playerAnimate.SetBool("isJumping",true); 
        }
        
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

        // 3. Animation Logic stays in Update for visual smoothness
        playerAnimate.SetFloat("Motion", moveInput.x);
        playerAnimate.SetFloat("Facing", facing);
        playerAnimate.SetBool("isRunning", isMoving && isGrounded);
    }

    void FixedUpdate()
    {
        // 4. Physics Logic ONLY in FixedUpdate, using Time.fixedDeltaTime
      if(isGrounded){ playerRB.linearVelocity = new Vector2(moveInput.x * moveSpeed, playerRB.linearVelocity.y); }
    }
}
