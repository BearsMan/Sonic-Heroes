using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fan : MonoBehaviour
{
    void OnTriggerStay(Collider other)
    {
        UltimatePlayerMovement player = other.GetComponent <UltimatePlayerMovement>();
        if (player)
        {

            player.Launch(Vector3.up, 5);




        }
    }
}
