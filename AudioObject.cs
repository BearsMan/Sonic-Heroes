using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioObject : MonoBehaviour
{
    private AudioSource source;
    private Transform followTarget;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (followTarget != null)
        {
            transform.position = followTarget.position;
        }
    }

    public void Setup(AudioClip sound, Transform target)
    {
        if (sound == null || target == null)
        {
            return;
        }
        
        source.Stop();

        followTarget = target;
        transform.position = target.position;

        source.clip = sound;
        source.pitch = Random.Range(0.95f, 1.05f);
        source.Play();

        Destroy (gameObject, sound.length);
    }
}