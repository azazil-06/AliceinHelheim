using UnityEngine;

public class FireflyFollower : MonoBehaviour
{
    public Transform player;
    
    // How long it takes the fireflies to catch up. Higher = softer tug
    public float smoothTime = 0.5f;
    
    // Where the fireflies should hover relative to the player
    public Vector3 offset = new Vector3(0, 1.5f, -1f);
    
    private Vector3 velocity = Vector3.zero;

    void Update()
    {
        if (player != null)
        {
            // CHANGED: TransformPoint converts the local offset into world space, 
            // factoring in the player's rotation and scale.
            Vector3 targetPosition = player.TransformPoint(offset);
            
            // Smoothly glide the emitter toward that position
            transform.position = Vector3.SmoothDamp(
                transform.position, 
                targetPosition, 
                ref velocity, 
                smoothTime
            );
        }
    }
}