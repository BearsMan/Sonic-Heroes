using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BarrierSoundClip : PickUpObject
{
    public AudioSource asource;
    public AudioClip BarrierSound;

    private IEnumerator PickUp()
    {
        HUD hud = UnityEngine.Object.FindAnyObjectByType<HUD>();
        if (hud != null)
            hud.AddPower(powerValue);
        GameInstance.currentRings += ringValue;
        vis.SetActive(false);
        GetComponent<Collider>().enabled = false;
        if (hud != null)
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

