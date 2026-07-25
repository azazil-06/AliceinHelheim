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
    private float facing;







    //least used
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
        moveInput = new Vector2 (control.Player.Move.ReadValue<Vector2>().x, 0f); //ignore y input

        
    }

    void FixedUpdate()
    {
        Vector2 position = (Vector2)playerRB.position + moveInput * moveSpeed * Time.deltaTime;
        playerRB.MovePosition(position);

        //animation
        playerAnimate.SetFloat("Motion", moveInput.x);
         
    }






}
