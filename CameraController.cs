using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public class CameraController : MonoBehaviour
{
    public Transform sonic;
    public Transform cam;
    public bool rayCastingOff;

    public Vector3 offset;

    public LayerMask cameraMask;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        Vector2 look = new Vector2();

        look.x = Input.GetAxis("Mouse X");
        look.y = Input.GetAxis("Mouse Y");

        transform.position = sonic.position - (transform.forward * 0.5f + sonic.up * 2);
        transform.rotation = Quaternion.LookRotation(Vector3.Cross(transform.right,sonic.up));

        float cameraDistance = 8;
        if (Physics.Raycast(transform.position, -transform.forward, out RaycastHit hit, 8.3f, cameraMask)&&rayCastingOff == false)
        {
            float distance = Vector3.Distance(transform.position, hit.point);
            cameraDistance = distance - 0.3f;
        }
        float ratio = cameraDistance / 8;
        cam.localPosition = new Vector3(0, 5 * ratio, -cameraDistance);



        Look(look);
    }

    private float xRotation;

    public void Look(Vector2 lookDirection)
    {

        transform.Rotate(transform.up, lookDirection.x*5);
        


        xRotation -= lookDirection.y; //Subtract mouse movement from xRotation(Subtract for invert movement)
        xRotation = Mathf.Clamp(xRotation, -30, 60); //Clamp rotation to prevent backflip
        cam.localRotation = Quaternion.Euler(xRotation, transform.localRotation.y, transform.rotation.z); //Apply xRotation to camera rotation

    }


}
