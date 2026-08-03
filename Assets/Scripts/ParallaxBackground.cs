using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    // is fixed when enabled fixes the frame to camera
    public bool isFixed;
    private Vector3 offset;

    private float startPos, length;
    public GameObject cam;
    public float parallaxEffect;  // speed at which bg moves w.r.t cam
    
    [Header("Damping Settings")]
    public float smoothing = 20f; // Lower = smoother/slower, Higher = tighter/faster

    void Start()
    {
        startPos = transform.position.x;
        offset = transform.position - cam.transform.position;
        if (!isFixed) { 
            length = GetComponent<SpriteRenderer>().bounds.size.x; 
        }
    }

    void LateUpdate()
    {
        if (isFixed)
        {
            // Calculate target position
            float targetX = cam.transform.position.x + offset.x;
            // Smoothly lerp towards target
            float smoothedX = Mathf.Lerp(transform.position.x, targetX, smoothing * Time.deltaTime);
            
            transform.position = new Vector3(smoothedX, transform.position.y, transform.position.z);
        }
        else
        {
            float distance = cam.transform.position.x * parallaxEffect; // parallax effect 0= move with cam, 1= static
            float movement = cam.transform.position.x * (1 - parallaxEffect);

            // Calculate where the background SHOULD be
            float targetX = startPos + distance;
            
            // Smoothly move towards that target position
            float smoothedX = Mathf.Lerp(transform.position.x, targetX, smoothing * Time.deltaTime);
            transform.position = new Vector3(smoothedX, transform.position.y, transform.position.z);

            // infinite scrolling
            if (movement > startPos + length)
            {
                startPos += length;
                // Snap the actual position forward so the Lerp doesn't cause a visible slide
                transform.position = new Vector3(transform.position.x + length, transform.position.y, transform.position.z);
            }
            else if (movement < startPos - length)
            {
                startPos -= length;
                // Snap the actual position backward
                transform.position = new Vector3(transform.position.x - length, transform.position.y, transform.position.z);
            }
        }
    }
}
