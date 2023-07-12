using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
public class TriggerActivator : MonoBehaviour
{
    public UnityEvent triggerEvent;
    private void OnTriggerEnter(Collider other)
    {
        if (!other.GetComponent<UltimatePlayerMovement>())
        {
            return;
        }
        triggerEvent.Invoke();

    }
}
