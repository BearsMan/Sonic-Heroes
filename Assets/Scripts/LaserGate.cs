using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Renderer))]
public class LaserGate : MonoBehaviour
{
    [SerializeField] private float onTime = 2f;
    [SerializeField] private float offTime = 2f;

    private Collider laserCollider;
    private Renderer laserRenderer;

    private void Awake()
    {
        laserCollider = GetComponent<Collider>();
        laserRenderer = GetComponent<Renderer>();

        if (!laserCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: LaserGate collider should have 'Is Trigger' enabled.");
        }
    }

    private void Start()
    {
        StartCoroutine(Blink());
    }

    private IEnumerator Blink()
    {
        while (true)
        {
            laserCollider.enabled = true;
            laserRenderer.enabled = true;

            yield return new WaitForSeconds(onTime);

            laserCollider.enabled = false;
            laserRenderer.enabled = false;

            yield return new WaitForSeconds(offTime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out UltimatePlayerMovement player))
        {
            Debug.Log("Player hit the laser!");
        }
    }
}