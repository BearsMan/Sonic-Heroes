using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InvinciblityPowerUp : PickUpObject
{
    
    public AudioClip invinciblityMusic;

    protected override void AddEffect()
    {
        HUD hud = FindFirstObjectByType<HUD>();
        hud.AddPower(powerValue);
        GameInstance.currentRings += ringValue;
        vis.SetActive(false);
        GetComponent<Collider>().enabled = false;
        hud.ShowPickUp(itemSprite);
        FindFirstObjectByType<MusicAudio>().PlayMusic(invinciblityMusic);

        StartCoroutine(SpeedBoost());
    }

    private IEnumerator SpeedBoost()
    {
        UltimatePlayerMovement player = FindFirstObjectByType<UltimatePlayerMovement>();
        //Add Invinciblity
        yield return new WaitForSeconds(55);
        //Remove Invinciblity
        Destroy(gameObject);
    }
}

