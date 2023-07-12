using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelUpCore : MonoBehaviour
{
    public float timeToKill;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(DeleteCore());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private IEnumerator DeleteCore()
    {
        yield return new WaitForSeconds(timeToKill);
        Destroy(gameObject);
    }
}
