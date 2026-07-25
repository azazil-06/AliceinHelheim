using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    
    //is fixed when enabled fixes the frame to camera
    public bool isFixed;
    private Vector3 offset;


    private float startPos , length;
    public GameObject cam;
    public float parallaxEffect;  //speed aat which bg moves w.r.t cam
                                //edit this var in inspector
    void Start()
    {
        startPos=transform.position.x;
        offset = transform.position - cam.transform.position;
        if(!isFixed){ length=GetComponent<SpriteRenderer>().bounds.size.x; }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (isFixed)
        {
            transform.position = new Vector3(cam.transform.position.x + offset.x,transform.position.y,transform.position.z);
        }




        else{
        float distance = cam.transform.position.x * parallaxEffect ; // parallax effect 0= move with cam, 1= static

        float movement = cam.transform.position.x * (1-parallaxEffect);

        transform.position = new Vector3(startPos+distance, transform.position.y,transform.position.z);


        // infinite scrolling
        if(movement >startPos+length)
        {
            startPos+=length;
        }
        else if(movement <startPos-length)
        {
            startPos-=length;
        }
        }
    
    }


}
