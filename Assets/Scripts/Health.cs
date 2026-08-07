using UnityEngine;

public class Health : MonoBehaviour
{
    private int health = 3;
    private float timeSinceLastHit = 5f;
    public bool dead;
    public HealthBar bar;
    // Start is called before the first frame update
    void Start()
    {

    }

    public int HealthValue
    {
        get { return health; }
        set
        {
            // If a large value is provided (like 100 for full restore), treat as full health
            if (bar != null && bar.bars != null && bar.bars.Length > 0)
            {
                int maxHealth = bar.bars.Length - 1;
                if (value > maxHealth) health = maxHealth;
                else health = Mathf.Clamp(value, 0, maxHealth);
                dead = (health == 0);
                if (!dead)
                {
                    // Ensure collider and railAnimator are active when healed
                    var col = GetComponent<Collider>();
                    if (col != null) col.enabled = true;
                    var anim = GetComponentInChildren<Animator>();
                    if (anim != null) anim.SetBool("Sleep", false);
                }
                bar.Hit(health);
            }
            else
            {
                health = value;
                dead = (health == 0);
            }
        }
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
            Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Exclude).AddPower(10);
            Destroy(gameObject, 4);

        }

    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.CompareTag("Player")) TakeDamage(1);
    }
}
