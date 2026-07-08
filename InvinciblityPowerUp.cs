using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InvinciblityPowerUp : PickUpObject
{
    
    public AudioClip invinciblityMusic;

    protected override void AddEffect()
    {
        HUD hud = Object.FindAnyObjectByType<HUD>();
        hud.AddPower(powerValue);
        GameInstance.currentRings += ringValue;
        vis.SetActive(false);
        GetComponent<Collider>().enabled = false;
        hud.ShowPickUp(itemSprite);
        var music = Object.FindAnyObjectByType<MusicAudio>();
        if (music != null) music.PlayMusic(invinciblityMusic);

        StartCoroutine(SpeedBoost());
    }

    private IEnumerator SpeedBoost()
    {
        UltimatePlayerMovement player = Object.FindAnyObjectByType<UltimatePlayerMovement>();
        //Add Invinciblity
        yield return new WaitForSeconds(55);
        //Remove Invinciblity
        Destroy(gameObject);
    }
}

