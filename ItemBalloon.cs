using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(SpriteRenderer))]
public class ItemBalloon : MonoBehaviour
{
    [Header("Item Box")]
    [Range(5, 20)]
    public int ringValue = 5;
    public GameObject levelUpHUD;
    public GameObject levelUpParent;
    public GameObject levelUpPrefab;
    public List<AudioClip> characterSFX = new List<AudioClip>();
    public bool levelingUp = false;
    public ItemType itemType;
    public Sprite pickUpSprite;
    public Sprite bluePowerCore, redPowerCore, yellowPowerCore;
    public Sprite shieldSprite;
    public Sprite invinciblitySprite;
    public Sprite extraLife;
    public Sprite teamBlastSprite;
    public Sprite keySprite;
    public Sprite emeraldSprite;
    private AudioSource ballonAudioPop;
    // Start is called before the first frame update
    void Start()
    {
        ballonAudioPop = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {

    }
    public void LevelUpSound()
    {
        if (ballonAudioPop == null)
        {
            return;
        }
        if (ballonAudioPop != null)
        {
            ballonAudioPop.Play();
        }
    }
    public void OnTriggerEnter(Collider other)
    {
        Debug.Log("Item Ballon triggered by:" + other.name);
        if (other.CompareTag("Player") && levelingUp == false)
        {
            Debug.Log("Player accepted the pick up");
            levelingUp = true;
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = false;
            }
            CharacterType character = other.GetComponentInChildren<CharacterType>();
            switch (itemType)
            {
                case ItemType.Rings:
                    if (character != null)
                    {
                        GameInstance.AddRings(character.type, ringValue);
                    }
                    break;

                case ItemType.SpeedCore:
                    GameInstance.speedCoreLevelUp++;
                    pickUpSprite = bluePowerCore;
                    break;

                case ItemType.FlyCore:
                    GameInstance.flyCorelevelUp++;
                    pickUpSprite = yellowPowerCore;
                    break;

                case ItemType.PowerCore:
                    GameInstance.powerLevelUpCore++;
                    pickUpSprite = redPowerCore;
                    break;

                case ItemType.Shield:
                    pickUpSprite = shieldSprite;
                    if (character != null)
                    {
                        GiveShield(character.gameObject);
                    }
                    break;

                case ItemType.Invinciblity:
                    pickUpSprite = invinciblitySprite;
                    break;

                case ItemType.TeamBlast:
                    pickUpSprite = teamBlastSprite;
                    
                    break;

                case ItemType.ExtraLife:
                    GameInstance.livesCount++;
                    pickUpSprite = extraLife;
                    break;

                case ItemType.SpecialKey:
                    pickUpSprite = keySprite;
                    
                    break;

                case ItemType.ChaosEmerald:
                    pickUpSprite = emeraldSprite;
                    // TODO: Award the Chaos Emerald
                    break;

                 default:
                    Debug.LogWarning("Unknown itemType" + itemType);
                    break;
            }

            if (levelUpPrefab != null && levelUpParent != null)
            {
                Instantiate(levelUpPrefab, levelUpParent.transform);
            }

            Debug.Log($"Ring Value={ringValue}");
            Debug.Log($"Pickup Sprite={(pickUpSprite == null ? "NULL" : pickUpSprite.name)}");
            HUD hud = FindAnyObjectByType<HUD>();
            if (hud != null)
            {
                hud.AddPower(5);

                if (pickUpSprite != null)
                {
                    hud.ShowPickUp(pickUpSprite);
                }

                hud.UpdateRings();
            }

            else
            {
                Debug.LogWarning("HUD not found.");
            }
            StartCoroutine(PlayLevelUpSFX());
        }
    }

    public void GiveShield(GameObject player)
    {
        if (player == null)
            return;

        PlayerShield shield = player.GetComponent<PlayerShield>();

        if (shield == null)
        {
            shield = player.AddComponent<PlayerShield>();
        }

        shield.ActivateShield();
    }

    public IEnumerator PlayLevelUpSFX()
    {
        AudioSource audio = GetComponent<AudioSource>();
        if (audio == null)
        {
            Destroy(gameObject);
            yield break;
        }

        audio.Play();
        while (audio.isPlaying)
        {
            yield return null;
        }

        if (characterSFX != null && GameInstance.currentTeam >= 0 && GameInstance.currentTeam < characterSFX.Count && characterSFX[GameInstance.currentTeam] != null)
        {
            audio.clip = characterSFX[GameInstance.currentTeam];
            audio.Play();

            while (audio.isPlaying)
            {
                yield return null;
            }
        }
        Destroy (gameObject);
    }
    public IEnumerator LevelUpCharacter(GameObject speedCharacter)
    {
        UltimatePlayerMovement playerMovement = speedCharacter.GetComponent<UltimatePlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.leftFollower.SetActive(true);
            playerMovement.rightFollower.SetActive(true);
        }
        speedCharacter.GetComponent<UltimatePlayerMovement>().leftFollower.SetActive(true);
        speedCharacter.GetComponent<UltimatePlayerMovement>().rightFollower.SetActive(true);


        yield return null;
    }

    /* This is the team selections setup from LevelUpCore.cs for Character Level Up Sound Effects to be played in correct team order
    // Team Sonic = 0
    // Team Dark = 1
    // Team Rose = 2
    // Team Chaotix = 3
    */
}
