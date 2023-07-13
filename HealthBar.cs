using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class HealthBar : MonoBehaviour
{
    private float timeSinceLastHit = 6f;

    public Sprite[] bars = new Sprite[3];
    public Sprite[] sleepSprite;
    public SpriteRenderer bar;
    bool sleeping = true;
    private void Awake()
    {
        
        StartCoroutine(Sleep());
    }
    // Update is called once per frame
    void Update()
    {
        timeSinceLastHit -= Time.deltaTime;

        if (timeSinceLastHit <= 0 && !sleeping)
        {
            bar.enabled = false;
        }
        transform.LookAt(Camera.main.transform);
    }

    public void Hit(int health)
    {
        StopAllCoroutines();
        sleeping = false;
        timeSinceLastHit = 5;
        bar.enabled = true;
        bar.sprite = bars[health];
    }

    public IEnumerator Sleep()
    {
        while (true)
        {
            bar.sprite = sleepSprite[0];
            yield return new WaitForSeconds(1);
            bar.sprite = sleepSprite[1];
            yield return new WaitForSeconds(1);
            bar.sprite = sleepSprite[2];
            yield return new WaitForSeconds(1);
            
            
        }
    }

}

