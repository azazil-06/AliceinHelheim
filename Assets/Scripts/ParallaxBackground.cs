using UnityEngine;

[DefaultExecutionOrder(200)] // Runs after CinemachineBrain (order 100) so camera position is current frame
public class ParallaxBackground : MonoBehaviour
{
    // is fixed when enabled fixes the frame to camera
    public bool isFixed;
    private Vector3 offset;

    private float startPos;
    private float camStartX;
    private float scrollOffset;
    private float length;
    public GameObject cam;
    public float parallaxEffect;  // speed at which bg moves w.r.t cam
    
    [Header("Damping Settings")]
    public float smoothing = 20f; // Lower = smoother/slower, Higher = tighter/faster

    void Start()
    {
        if (cam == null && Camera.main != null)
        {
            cam = Camera.main.gameObject;
        }

        startPos = transform.position.x;
        if (cam != null)
        {
            camStartX = cam.transform.position.x;
            offset = transform.position - cam.transform.position;
        }

        if (!isFixed)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                length = sr.bounds.size.x;
            }
        }
    }

    void LateUpdate()
    {
        if (cam == null) return;

        if (isFixed)
        {
            float targetX = cam.transform.position.x + offset.x;
            float factor = smoothing > 0f ? (1f - Mathf.Exp(-smoothing * Time.deltaTime)) : 1f;
            float smoothedX = Mathf.Lerp(transform.position.x, targetX, factor);
            transform.position = new Vector3(smoothedX, transform.position.y, transform.position.z);
        }
        else
        {
            float camDelta = cam.transform.position.x - camStartX;
            float distance = camDelta * parallaxEffect;
            float movement = camDelta * (1f - parallaxEffect);

            if (length > 0.001f)
            {
                // Infinite scrolling wrapping
                if (movement > scrollOffset + length)
                {
                    scrollOffset += length;
                    transform.position = new Vector3(transform.position.x + length, transform.position.y, transform.position.z);
                }
                else if (movement < scrollOffset - length)
                {
                    scrollOffset -= length;
                    transform.position = new Vector3(transform.position.x - length, transform.position.y, transform.position.z);
                }
            }

            float targetX = startPos + scrollOffset + distance;
            float factor = smoothing > 0f ? (1f - Mathf.Exp(-smoothing * Time.deltaTime)) : 1f;
            float smoothedX = Mathf.Lerp(transform.position.x, targetX, factor);
            transform.position = new Vector3(smoothedX, transform.position.y, transform.position.z);
        }
    }
}
