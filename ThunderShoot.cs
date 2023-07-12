using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThunderShoot : MonoBehaviour
{
    public GameObject flyCharacter;
    public float thunderStrike = 0.0f;
    public bool isShooting = false;
    public float shooting = 0.0f;
    private float shootingSpeed = 0.0f;
    public float shootingDirection = 0.0f;
    // Start is called before the first frame update
    void Start()
    {
        Rigidbody rigidbody = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void CallThunderShoot()
    {
        FindFirstObjectByType<UltimatePlayerMovement>().CompareTag("Player");
        if (isShooting == true && Input.GetKeyDown(KeyCode.X))
        {
            
        }
    }
    public IEnumerator ThunderShootAttack(GameObject flyCharacter)
    {
        GetComponent<UltimatePlayerMovement>().leftFollower.SetActive(true);
        GetComponent<UltimatePlayerMovement>().rightFollower.SetActive(true);

        yield return null;
    }
}