using UnityEngine;

public class ArmoredTrain : MonoBehaviour
{
    private float speed = 10f;
    [SerializeField, Min(1f)] private float health = 100f;

    private bool destroyed;

    private void Update()
    {
        if (destroyed)
        {
            return;
        }

        Move();
    }

    private void Move()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self);
    }

    public void TakeDamage(float damage)
    {
        if (destroyed || damage <= 0f)
        {
            return;
        }

        health = Mathf.Max(0f, health - damage);

        if (health > 0f)
        {
            return;
        }

        destroyed = true;
        Destroy (gameObject);
    }
}