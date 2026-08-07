using System.Collections;
using UnityEngine;

public class Emblem : MonoBehaviour
{
    public AudioClip clip;
    GameObject source;
    public bool levelrunning = false;
    public static float timeelapsed = 0;

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(LevelTimer());
        source = Resources.Load<GameObject>("Audio Object");


    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("End Level");
        GameObject ao = Instantiate(source, transform.position, Quaternion.identity);
        if (ao != null && ao.TryGetComponent<AudioObject>(out var aObj)) aObj.Setup(clip, transform);
    }

    public IEnumerator LevelTimer()
    {
        while (levelrunning == true)
        {

            timeelapsed += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }



    }

}
