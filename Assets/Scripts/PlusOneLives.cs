using System.Collections;
using UnityEngine;

public class PlusOneLives : PickUpObject
{
    public AudioClip oneLivesUp;
    protected override void AddEffect(CHARACTERTYPES charactherTypes)
    {
        base.AddEffect(charactherTypes);

        GameInstance.livesCount++;
        StartCoroutine(DestroyAfterPlay());

    }

    private IEnumerator DestroyAfterPlay()
    {
        AudioSource source = GetComponent<AudioSource>();
        yield return new WaitForSecondsRealtime(0.1f);
        while (source.isPlaying)
        {
            yield return new WaitForEndOfFrame();
        }

        Destroy(gameObject);
    }
}

