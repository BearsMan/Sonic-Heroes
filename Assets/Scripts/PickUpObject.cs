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
        if (collected || other == null)
        {
            return;
        }

        CharacterType characterType = null;

        UltimatePlayerMovement playerMovement = other.GetComponentInParent<UltimatePlayerMovement>();

        if (playerMovement != null)
        {
            if (playerMovement.currentCharacter != null)
            {
                characterType = playerMovement.currentCharacter.GetComponent<CharacterType>();

                if (characterType == null)
                {
                    characterType = playerMovement.currentCharacter.GetComponentInChildren<CharacterType>();
                }

                if (characterType == null)
                {
                    characterType = playerMovement.currentCharacter.GetComponentInParent<CharacterType>();
                }
            }

            if (characterType == null)
            {
                characterType = other.GetComponentInParent<CharacterType>();
            }
        }
        else
        {
            FollowerNavigation followerNavigation = other.GetComponentInParent<FollowerNavigation>();

            if (followerNavigation != null)
            {
                characterType = other.GetComponentInParent<CharacterType>();

                if (characterType == null)
                {
                    characterType = followerNavigation.GetComponentInChildren<CharacterType>();
                }
            }
        }

        if (characterType == null)
        {
            return;
        }

        collected = true;

        AddEffect(characterType.type);
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
