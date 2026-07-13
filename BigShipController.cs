using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BigShipController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 15f;
    [SerializeField] private float turnSpeed = 2f;

    private Rigidbody body;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        Vector3 nextPosition = body.position + transform.up * moveSpeed * Time.fixedDeltaTime;
        Quaternion nextRotation = body.rotation * Quaternion.Euler(0f, turnSpeed * Time.fixedDeltaTime, 0f);

        body.MovePosition(nextPosition);
        body.MoveRotation(nextRotation);
    }
}
