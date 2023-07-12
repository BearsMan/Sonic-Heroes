using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeGate : MonoBehaviour
{
    public CHARACTERTYPES gateType = CHARACTERTYPES.Speed;
    public GameObject speed, fly, power;
    // Start is called before the first frame update
    void Start()
    {
        switch (gateType)
        {
            case CHARACTERTYPES.Speed:
                speed.SetActive(true);
                break;
            case CHARACTERTYPES.Fly:
                fly.SetActive(true);
                break;
            case CHARACTERTYPES.Power:
                power.SetActive(true);
                break;
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            FindFirstObjectByType<CharacterSwitch>().SetCharacter(gateType);
        }
    }   
}
