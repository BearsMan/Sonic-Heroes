using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(SpriteRenderer))]
public class LevelUp : MonoBehaviour
{
    public GameObject levelUpParent;
    public GameObject levelUpPrefab;
    public List<AudioClip> characterSFX = new List<AudioClip>();

    public bool levelingUp;

    public void LevelUpSound()
    {
        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null) audio.Play();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || levelingUp)
            return;

        levelingUp = true;

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.enabled = false;

        GameInstance.speedLevelUp += 1;
        GameInstance.flyLevelUp += 1;
        GameInstance.powerLevelUp += 1;
        if (levelUpPrefab != null && levelUpParent != null)
        {
            GameObject levelUpInstance = Instantiate(levelUpPrefab, levelUpParent.transform);
            levelUpInstance.transform.localPosition = Vector3.zero;
        }
        HUD hud = FindAnyObjectByType<HUD>(FindObjectsInactive.Exclude);
        if (hud != null)
        {
            hud.AddPower(5);
        }

        StartCoroutine(PlayLevelUpSFX());
    }

    private IEnumerator PlayLevelUpSFX()
    {
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Destroy(gameObject);
            yield break;
        }

        audioSource.Play();

        while (audioSource.isPlaying)
        {
            yield return null;
        }

        if (characterSFX != null &&
            GameInstance.currentTeam >= 0 &&
            GameInstance.currentTeam < characterSFX.Count &&
            characterSFX[GameInstance.currentTeam] != null)
        {
            audioSource.clip = characterSFX[GameInstance.currentTeam];
            audioSource.Play();

            while (audioSource.isPlaying)
            {
                yield return null;
            }
        }

        Destroy(gameObject);
    }

    public IEnumerator LevelUpCharacter(GameObject speedCharacter)
    {
        if (speedCharacter != null)
        {
            TeamActionController controller = speedCharacter.GetComponent<TeamActionController>();

            if (controller == null)
            {
                controller = speedCharacter.GetComponentInParent<TeamActionController>();
            }

            controller?.EnableFollowers();
        }

        yield return null;
    }
}