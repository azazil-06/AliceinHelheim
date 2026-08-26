using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LegMover : MonoBehaviour
{
    [Header("Scene References (must assign in Inspector)")]
    public Transform legTarget;      // IK effector for this leg (can be child of body or separate)
    public LayerMask groundLayer;    // layer your ground colliders use
    public LegMover opposingLeg;     // drag the OTHER leg's LegMover component here
    public AudioSource aud;          // auto-filled in Start(), leave empty in Inspector

    [Header("Tuning Values (test defaults)")]
    public float hoverDist = 1.0f;
    public float groundCheckDistance = 5f;
    public float legMoveDist = 0.6f;
    public float liftDistance = 0.3f;
    public float legMovementSpeed = 10f;

    [Header("Runtime State (leave at defaults)")]
    public int posIndex = 0;
    public Vector3 targetPoint;
    public Vector3 halfWayPoint;
    public bool grounded;

    Vector3 desiredPosition;         // world-space ground position the leg should step towards
    Vector3 lastSteppedBodyPos;      // body position at the time of the last completed step
    Vector3 lastSteppedLegWorldPos;  // legTarget world position at last step (so we can re-apply it)

    // Start is called before the first frame update
    void Start()
    {
        aud = GetComponent<AudioSource>();
        desiredPosition = transform.position;
        lastSteppedBodyPos = transform.position;
        lastSteppedLegWorldPos = legTarget.position;
    }

    // Update is called once per frame
    void Update()
    {
        CheckGround();

        // If no opposing leg is assigned, treat it as always grounded (allows single-leg testing)
        bool opposingLegGrounded = (opposingLeg == null) || opposingLeg.grounded;

        // Use how far the BODY has moved since the last step as the trigger.
        // This works correctly whether legTarget is a child of the body or not.
        float bodyTravelDist = Vector2.Distance(transform.position, lastSteppedBodyPos);

        if (bodyTravelDist > legMoveDist && posIndex == 0 && opposingLegGrounded)
        {
            // Capture legTarget's current world position before we start moving it
            Vector3 currentLegWorldPos = legTarget.position;

            targetPoint = desiredPosition;
            halfWayPoint = (targetPoint + currentLegWorldPos) / 2;
            halfWayPoint.y += liftDistance;
            posIndex = 1;

            lastSteppedBodyPos = transform.position;
        }

        else if (posIndex == 1)
        {
            legTarget.position = Vector3.Lerp(legTarget.position, halfWayPoint, legMovementSpeed * Time.deltaTime);

            if (Vector2.Distance(legTarget.position, halfWayPoint) <= 0.1f)
            {
                posIndex = 2;
            }
        }

        else if (posIndex == 2)
        {
            legTarget.position = Vector3.Lerp(legTarget.position, targetPoint, legMovementSpeed * Time.deltaTime);

            if (Vector2.Distance(legTarget.position, targetPoint) < 0.1f)
            {
                if (aud != null)
                {
                    aud.pitch = Random.Range(1.8f, 1.9f);
                    aud.Play();
                }
                posIndex = 0;
            }
        }

        if (posIndex == 0)
        {
            grounded = true;
        }
        else
        {
            grounded = false;
        }
    }

    public void CheckGround()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
        if (hit.collider != null)
        {
            Vector3 point = hit.point;
            point.y += hoverDist;
            desiredPosition = point;
        }
        else
        {
            // No ground hit — fall back to transform.position (body position)
            desiredPosition = transform.position;
        }
    }
}
