using UnityEngine;

public class TrickZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        UltimatePlayerMovement player = other.GetComponent<UltimatePlayerMovement>();
        if (player)
        {
            player.SetTrickZoneActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        UltimatePlayerMovement player = other.GetComponent<UltimatePlayerMovement>();
        if (player)
        {
            player.SetTrickZoneActive(false);
        }
    }
}
