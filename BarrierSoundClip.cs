using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BarrierSoundClip : PickUpObject
{
    public AudioSource asource;
    public AudioClip BarrierSound;

    [System.Obsolete]
    private IEnumerator PickUp()
    {
        HUD hud = FindObjectOfType<HUD>();
        hud.AddPower(powerValue);
        GameInstance.currentRings += ringValue;
        vis.SetActive(false);
        GetComponent<Collider>().enabled = false;
        hud.ShowPickUp(itemSprite);
        AudioSource source = GetComponent<AudioSource>();
        source.Play();
        while (source.isPlaying)
        {
            yield return new WaitForEndOfFrame();
        }
        Destroy(gameObject);
    }
}

