using System.Collections;
using UnityEngine;

public class LevelUpUI : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    public IEnumerator DestoryObject()
    {
        yield return new WaitForSeconds(2);
    }
}
