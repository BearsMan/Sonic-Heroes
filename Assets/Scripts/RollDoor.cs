using System.Collections;
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
        player.DisableMovement();

        yield return new WaitForSeconds(1f);

        player.EnableMovement();

        player.LaunchFromSpring(
            direction.forward * 30f);
    }
}
