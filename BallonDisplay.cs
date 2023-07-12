using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallonDisplay : MonoBehaviour
{
    public List<GameObject> myBallonItems;
    public GameObject myBallonDisplayPopUp;
    public bool myBallonVisible = false;
    public float myBallonDistance = 0.0f;
    public int myBallonNumber = 0;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        FindFirstObjectByType<PickUpObject>();
        {
            myBallonItems.Clear();
            if (myBallonDisplayPopUp != null)
            {
                myBallonNumber = 0;
            }
        }
        
    }
   
}
