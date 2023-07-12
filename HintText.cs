using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class HintText : MonoBehaviour
{
    public TextAsset hintText;
    public GameObject gameHint;
    public float hintTime;
    public void ShowHint(string hint)
    {
       
    }

    public void OnTriggerEnter(Collider other)
    {
        StartCoroutine(HintTimer());
    }
    public IEnumerator HintTimer()
    {
        gameHint.SetActive(true);
        gameHint.GetComponent<TextMeshPro>().text = hintText.text;
        
        yield return new WaitForSeconds(hintTime);
        gameHint.SetActive(false);
    }
}
