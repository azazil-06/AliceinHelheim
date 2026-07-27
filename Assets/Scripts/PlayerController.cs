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

    //hardcoded var
    public Vector2 moveInput;
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

        // 2. Uniform Deadzone for both directions (prevents input jitter/flicker)
        bool isMoving = Mathf.Abs(moveInput.x) > 0.1f;
        
        if (isMoving)
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
        playerAnimate.SetBool("isRunning", isMoving);
    }

    void FixedUpdate()
    {
        // 4. Physics Logic ONLY in FixedUpdate, using Time.fixedDeltaTime
        Vector2 position = (Vector2)playerRB.position + moveInput * moveSpeed * Time.fixedDeltaTime;
        playerRB.MovePosition(position);
    }
}
