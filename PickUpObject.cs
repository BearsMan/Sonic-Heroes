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
        HUD hud = FindFirstObjectByType<HUD>();
        hud.AddPower(powerValue);
        GameInstance.currentRings += ringValue;
        vis.SetActive(false);
        GetComponent<Collider>().enabled = false;
        hud.ShowPickUp(itemSprite);
        AudioSource source = GetComponent<AudioSource>();
        source.Play();
    }

    
}
