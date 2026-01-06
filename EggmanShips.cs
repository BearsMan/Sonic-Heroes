using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EggmanShips : MonoBehaviour
{
    public EggmanShips smallship;
    public EggmanShips mediumShips;
    public EggmanShips largeships;
    public float speed = 5f;
    public bool isMoving = false;
    private bool isGrounded = false;
    private Rigidbody body;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        body = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void FixedUpdate()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 move = new Vector3 (horizontal, vertical, 0).normalized;
        body.MovePosition(move);
    }
}
