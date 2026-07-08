using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickUpObject : MonoBehaviour
{
    public int ringValue = 0;
    public int powerValue = 0;
    public GameObject vis;
    public Sprite itemSprite;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out UltimatePlayerMovement player))
        {
            AddEffect();
        }
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, 90 * Time.deltaTime);
    }

    protected virtual void AddEffect()
    {
        // Look up HUD explicitly and only interact with it if present to avoid null references.
        HUD hud = Object.FindAnyObjectByType<HUD>();
        if (hud != null)
        {
            hud.AddPower(powerValue);
            hud.ShowPickUp(itemSprite);
        }

        // Always apply game state changes and visuals/audio regardless of HUD presence.
        GameInstance.currentRings += ringValue;
        vis.SetActive(false);
        GetComponent<Collider>().enabled = false;
        AudioSource source = GetComponent<AudioSource>();
        source.Play();
    }

    
}
