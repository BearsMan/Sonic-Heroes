using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FallReset : MonoBehaviour
{
    [SerializeField] private Transform resetPoint;
    [SerializeField] private float fallLimit = -30f;

    private Rigidbody body;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (resetPoint == null || transform.position.y >= fallLimit)
        {
            return;
        }

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = resetPoint.position;
        }
        else
        {
            transform.position = resetPoint.position;
        }
    }
}