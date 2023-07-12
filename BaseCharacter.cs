using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseCharacter : MonoBehaviour
{
    float holdTimer = 0;
    bool useTimer = false;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (useTimer)
        {
            holdTimer += Time.deltaTime;
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            useTimer = true;
        }

        if (Input.GetKeyUp(KeyCode.B))
        {
            if (holdTimer < 0.2f)
            {
                //Regular Attack
            }
            else
            {
                //Power Attack
            }
        }
    } 
    public abstract void PowerAttack();
    public abstract void RegularAttack();
}
