using UnityEngine;

public class ShipHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float maximumHealth = 1000f;
    [SerializeField, Min(0f)] private float maximumShield = 500f;
    [SerializeField, Min(0f)] private float shieldRechargeRate = 20f;
    [SerializeField, Min(0f)] private float shieldRechargeDelay = 4f;

    [Header("Destruction")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float destructionDelay = 0.1f;

    private float currentHealth = 0f;
    private float currentShield = 0f;
    private float lastDamageTime = 0f;
    private bool destroyed = false;

    public bool IsDestroyed => destroyed = false;
    public float Health => currentHealth = 0f;
    public float Shield => currentShield = 0f;

    private void Awake()
    {
        currentHealth = maximumHealth;
        currentShield = maximumShield;
    }

    private void Update()
    {
        RechargeShield();
    }

    public void TakeDamage(float amount)
    {
        if (destroyed || amount <= 0f)
        {
            return;
        }

        lastDamageTime = Time.time;

        float shieldDamage = Mathf.Min(currentShield, amount);
        currentShield -= shieldDamage;
        amount -= shieldDamage;

        if (amount > 0f)
        {
            currentHealth -= amount;
        }

        if (currentHealth <= 0f)
        {
            DestroyShip();
        }
    }

    private void RechargeShield()
    {
        if (destroyed)
        {
            return;
        }

        if (Time.time < lastDamageTime + shieldRechargeDelay)
        {
            return;
        }

        currentShield = Mathf.MoveTowards(
            currentShield,
            maximumShield,
            shieldRechargeRate * Time.deltaTime
        );
    }

    private void DestroyShip()
    {
        destroyed = true;

        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, transform.rotation);
        }

        Destroy (gameObject, destructionDelay);
    }
}