using UnityEngine;

public class Crate : MonoBehaviour
{
    public GameObject[] spawnable = new GameObject[0];
    public AudioClip breakSound;
    private bool HasPlayedCrateSound = false;
    public void DestroyCrate()
    {
        if (HasPlayedCrateSound)
        {
            return;
        }
        HasPlayedCrateSound = true;

        int num = Random.Range(0, spawnable.Length);
        Instantiate(spawnable[num], transform.position, transform.rotation);

        GameObject source = Resources.Load<GameObject>("AudioObject");
        if (source != null)
        {
            source = Instantiate(source, transform.position, transform.rotation);

            if (source.TryGetComponent<AudioObject>(out var srcObj))
            {
                srcObj.Setup(breakSound, transform);
            }
        }

        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<UltimatePlayerMovement>())
        {
            DestroyCrate();
        }
    }
}
