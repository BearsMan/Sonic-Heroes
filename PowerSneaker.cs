using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PowerSneaker : PickUpObject
{
    public AudioClip powerSneakerAudio;
    protected override void AddEffect(CHARACTERTYPES characterTypes)
    {
        HUD hud = Object.FindAnyObjectByType<HUD>();
        hud.AddPower(powerValue);
        GameInstance.currentRings += ringValue;
        vis.SetActive(false);
        GetComponent<Collider>().enabled = false;
        hud.ShowPickUp(itemSprite);
        var music = Object.FindAnyObjectByType<MusicAudio>();
        if (music != null) music.PlayMusic(powerSneakerAudio);

        StartCoroutine(SpeedBoost());
    }

    private IEnumerator SpeedBoost()
    {
        UltimatePlayerMovement player = Object.FindAnyObjectByType<UltimatePlayerMovement>();
        yield return new WaitForSeconds(15);
        Destroy(gameObject);
    }
}





