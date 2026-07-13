using UnityEngine;

public class Ring : MonoBehaviour
{
    GameObject source;
    public AudioClip clip;
    public Collider physicalCollider;
    private bool phased = false;
    // Start is called before the first frame update
    void Start()
    {
        source = Resources.Load<GameObject>("Audio Object");
        // Using the Resources folder in the Assests folder you can load up any kind of object
        // With this above line of code. It loads up a GameObject named "Audio Object" From the Resources folder 
        // And applies it to the source variable
    }



    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Ring Trigger fired" + other.name);
        if (phased)
        {
            return;
        }

        UltimatePlayerMovement player = other.GetComponent<UltimatePlayerMovement>();
        FollowerNavigation ai = other.GetComponent<FollowerNavigation>();
        if (player || ai)
        {
            GameObject ao = Instantiate(source, transform.position, Quaternion.identity);
            if (ao != null && ao.TryGetComponent<AudioObject>(out var audioObj))
            {
                audioObj.Setup(clip, transform);
            }

            GameInstance.AddRings(other.GetComponentInChildren<CharacterType>().type);
            Debug.Log("Rings after pickup" + GameInstance.currentRings);
            var hud = Object.FindAnyObjectByType<HUD>();

            if (hud != null)
            {
                hud.UpdateRings();
                hud.AddPower(1);
            }

            Destroy (gameObject);
        }

    }

    private void EndPhase()
    {
        phased = false;
    }

    public void StartPhase()
    {
        physicalCollider.isTrigger = true;
        phased = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out UltimatePlayerMovement player) && phased)
        {
            physicalCollider.isTrigger = false;
            Invoke("EndPhase", 1);
        }
    }
}
