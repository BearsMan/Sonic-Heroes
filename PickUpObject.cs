using UnityEngine;
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Collider))]
public class PickUpObject : MonoBehaviour
{
    private bool collected = false;
    public int ringValue = 0;
    public int powerValue = 0;
    public GameObject vis;
    public Sprite itemSprite;


    private void OnTriggerEnter(Collider other)
    {
        Debug.LogWarning("PickUpObject: OnTriggerEnter called with " + other.name);
        if (collected)
        {
            return;
        }

        UltimatePlayerMovement playerMovement = other.GetComponentInParent<UltimatePlayerMovement>();
        FollowerNavigation followerNavigation = other.GetComponentInParent<FollowerNavigation>();
        Debug.LogWarning("PickUpObject: playerMovement is " + (playerMovement != null ? "not null" : "null") + ", followerNavigation is " + (followerNavigation != null ? "not null" : "null"));
        CharacterType teamCharacters = null;

        if (playerMovement != null && playerMovement.currentCharacter != null)
        {
            teamCharacters = playerMovement.currentCharacter.GetComponent<CharacterType>();

        }
        else if (followerNavigation != null)
        {
            teamCharacters = followerNavigation.GetComponentInChildren<CharacterType>();
        }
            Debug.LogWarning("PickUpObject: teamCharacters is " + (teamCharacters != null ? "not null" : "null"));

        if (teamCharacters != null)
        {
            collected = true;
            AddEffect(teamCharacters.type);
        }
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, 90 * Time.deltaTime);
    }

    protected virtual void AddEffect(CHARACTERTYPES characterTypes)
    {
        HUD hud = FindAnyObjectByType<HUD>();
        if (hud != null)
        {
            hud.AddPower(powerValue);
            hud.ShowPickUp(itemSprite);
        }

        if (ringValue > 0)
        {
           GameInstance.AddRings(characterTypes, ringValue);
        }

        vis.SetActive(false);
        if (TryGetComponent<Collider>(out var pickUpItem))
        {
            pickUpItem.enabled = false;
        }

        if (TryGetComponent<AudioSource>(out var pickupSource))
        {
            pickupSource.Play();
        }
    }
}
