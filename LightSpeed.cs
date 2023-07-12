using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightSpeed : MonoBehaviour
{
    public bool lightSpeedAttack;
    public bool ringTrail;
    public List<GameObject> rings = new List<GameObject>();
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void DashPrep(GameObject speedCharacter)
    {
        string charname = speedCharacter.gameObject.name;
        if (charname != "Sonic The Hedgehog" || charname != "Shadow The Hedgehog" || charname != "Super Sonic")
        {
            return;
        }
        speedCharacter.GetComponent<Rigidbody>().useGravity = false;
        speedCharacter.GetComponentInParent<UltimatePlayerMovement>().body.velocity = Vector3.zero;
        speedCharacter.GetComponent<Animator>().Play("Light Speed Attack");
        StartCoroutine(Dash(speedCharacter));
    }

    public IEnumerator Dash(GameObject SpeedCharacter)
    {
        int counter = 0;
        while (counter < rings.Count)
        {
            SpeedCharacter.transform.position = rings[counter].transform.position;

            yield return new WaitForSeconds(0.1f);
            counter += 1;
        }
        DashExit(SpeedCharacter);
    }
    public void DashExit(GameObject SpeedCharacter)
    {
        SpeedCharacter.GetComponent<Rigidbody>().useGravity = true;
        SpeedCharacter.GetComponentInParent<UltimatePlayerMovement>().body.velocity = Vector3.zero;
        SpeedCharacter.GetComponent<Animator>().Play("Jump Down");

    }
    public void OnTriggerStay(Collider other)
    {
        
        if (other.GetComponent<UltimatePlayerMovement>() == true && Input.GetKey(KeyCode.B))
        {
            DashPrep(other.gameObject);
        }
    }
}

