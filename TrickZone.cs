using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrickZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        UltimatePlayerMovement player = other.GetComponent<UltimatePlayerMovement>();
        if (player)
        {
            player.TrickZone = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        UltimatePlayerMovement player = other.GetComponent<UltimatePlayerMovement>();
        if (player)
        {
            player.TrickZone = false;
        }
    }
}
