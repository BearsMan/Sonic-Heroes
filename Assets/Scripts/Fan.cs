using UnityEngine;

public class Fan : MonoBehaviour
{
    void OnTriggerStay(Collider other)
    {
        UltimatePlayerMovement player = other.GetComponent<UltimatePlayerMovement>();
        if (player)
        {

            player.LaunchFromSpring(
    Vector3.up * 5f);




        }
    }
}
