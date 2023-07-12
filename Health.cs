using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Health : MonoBehaviour
{
    private int health = 3;
    private float timeSinceLastHit = 5;
    public bool dead;
    public HealthBar bar;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    public void TakeDamage(int damage)
    {
        if (dead) return;
        GetComponentInChildren<Animator>().SetBool("Sleep", false);
        health = Mathf.Max(0, health - damage);
        bar.Hit(health);
        if (health == 0)
        {
            dead = true;
            GetComponentInChildren<Animator>().SetTrigger("Die");
            GetComponent<Collider>().enabled = false;
            FindFirstObjectByType<HUD>().AddPower(10);
            Destroy(gameObject, 4);

        }

    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.CompareTag("Player")) TakeDamage(1);
    }
}
