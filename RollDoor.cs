using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RollDoor : MonoBehaviour
{
    public GameObject powerCharacterTeamPull;
    public Transform direction;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void Hit(UltimatePlayerMovement player)
    {
        StartCoroutine(LaunchPlayer(player));

    }

    private IEnumerator LaunchPlayer(UltimatePlayerMovement player)
    {
        player.SurrenderControl(Vector3.zero, 2.5f);

        yield return new WaitForSeconds(1);
        player.Launch(direction.forward, 30);
    }
}
