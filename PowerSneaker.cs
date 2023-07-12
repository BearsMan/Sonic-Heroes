using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerSneaker : PickUpObject
{
    public AudioClip powerSneakerAudio;
    protected override void AddEffect()
    {
        HUD hud = FindFirstObjectByType<HUD>();
        hud.AddPower(powerValue);
        GameInstance.currentRings += ringValue;
        vis.SetActive(false);
        GetComponent<Collider>().enabled = false;
        hud.ShowPickUp(itemSprite);
        FindFirstObjectByType<MusicAudio>().PlayMusic(powerSneakerAudio);

        StartCoroutine(SpeedBoost());
    }

    private IEnumerator SpeedBoost()
    {
        UltimatePlayerMovement player = FindFirstObjectByType<UltimatePlayerMovement>();
        player.currentSpeed = player.currentSpeed + 5;
        yield return new WaitForSeconds(15);
        player.currentSpeed = 20;
        Destroy(gameObject);
    }
}





